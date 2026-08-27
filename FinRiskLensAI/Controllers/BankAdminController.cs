using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.Admin;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Models;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Bank-side portal: portfolio dashboard, onboarded-customer list and the
    /// per-customer 360 view.
    /// <para>
    /// Every action here requires an authenticated bank user, so the guard sits on
    /// the class rather than being repeated per action — there is no anonymous
    /// surface to carve out. Sign-in and sign-out live in AuthController alongside
    /// the customer login (<c>Auth/BankLogin</c>, <c>Auth/BankLogout</c>).
    /// </para>
    /// </summary>
    [BankAdminAuthorize]
    public class BankAdminController : Controller
    {
        private readonly IBankAdminService _bankAdmin;
        private readonly IBlobAnalysisService _analysis;
        private readonly CustomerProfileBuilder _profile;
        private readonly ILogger<BankAdminController> _logger;

        public BankAdminController(IBankAdminService bankAdmin, IBlobAnalysisService analysis,
            CustomerProfileBuilder profile, ILogger<BankAdminController> logger)
        {
            _bankAdmin = bankAdmin;
            _analysis = analysis;
            _profile = profile;
            _logger = logger;
        }

        /// <summary>Portfolio dashboard — KPIs and charts across all onboarded MSMEs.</summary>
        [HttpGet]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var model = new BankDashboardViewModel();
            try
            {
                model.Stats = await _bankAdmin.GetPortfolioStatsAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading bank portfolio stats");
                model.LoadError = "Could not load the portfolio summary right now. Please try again.";
            }
            return View(model);
        }

        /// <summary>Searchable, filterable list of every onboarded MSME.</summary>
        [HttpGet]
        public async Task<IActionResult> Customers(
            string? search, string? band, string? state, string? status,
            string sortBy = "onboarded", bool sortDesc = true,
            int page = 1, int pageSize = 25, CancellationToken ct = default)
        {
            var model = new BankCustomersViewModel
            {
                Query = new BankCustomerQuery
                {
                    Search = search, Band = band, State = state, Status = status,
                    SortBy = sortBy, SortDesc = sortDesc, Page = page, PageSize = pageSize
                }
            };

            try
            {
                model.Page = await _bankAdmin.GetCustomersAsync(model.Query, ct);
                model.States = await _bankAdmin.GetStatesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading bank customer list");
                model.LoadError = "Could not load the customer list right now. Please try again.";
            }

            return View(model);
        }

        /// <summary>
        /// Customer 360 — the full file on one MSME: Financial Health Card, Udyam
        /// identity, GST returns, MCA corporate report and the source-IP audit.
        /// Every section is built by CustomerProfileBuilder, the same code path the
        /// customer's own screens use, so the bank sees identical data.
        /// <para>
        /// The UAN comes from the route and is guarded by BankAdminAuthorize; the
        /// customer-facing pages stay session-scoped, so an MSME still cannot read
        /// anyone else's file.
        /// </para>
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Customer(string uan, string? tab, CancellationToken ct)
        {
            var model = new BankCustomerDetailViewModel
            {
                Uan = uan?.Trim(),
                ActiveTab = string.IsNullOrWhiteSpace(tab) ? "score" : tab.Trim().ToLowerInvariant()
            };

            if (string.IsNullOrWhiteSpace(model.Uan))
                return RedirectToAction(nameof(Customers));

            try
            {
                model.Customer = await _bankAdmin.GetCustomerAsync(model.Uan, ct);
                if (model.Customer == null)
                {
                    model.LoadError = $"No onboarded MSME found for {model.Uan}.";
                    return View(model);
                }

                // Same source the customer's own health card reads — the blob result.json
                model.Card = new FinancialHealthCardViewModel
                {
                    Uan = model.Uan,
                    EnterpriseName = model.Customer.EnterpriseName,
                    Status = await _analysis.GetStatusAsync(model.Uan, ct),
                    Result = await _analysis.GetResultAsync(model.Uan, ct)
                };

                // The remaining sections come from the shared builder. They are
                // independent, so a failure in one must not blank the whole page —
                // each view model carries its own LoadError for the tab to show.
                model.Udyam = await _profile.GetUdyamAsync(model.Uan);
                model.Gst = await _profile.GetGstAsync(model.Uan,
                    model.Customer.EnterpriseName, model.Customer.PanNumber);
                model.Mca = await _profile.GetMcaAsync(model.Uan, ct);
                model.IpAudit = await _profile.GetIpAuditAsync(model.Uan, model.Customer.IPAddress, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Customer 360 load canceled for {Uan}", model.Uan);
                model.LoadError = "The request timed out while loading this customer. Please refresh.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading Customer 360 for {Uan}", model.Uan);
                model.LoadError = "Could not load this customer right now. Please try again.";
            }

            return View(model);
        }
    }
}
