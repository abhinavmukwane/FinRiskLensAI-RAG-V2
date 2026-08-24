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
    /// Bank-side portal: login, portfolio dashboard, onboarded-customer list and
    /// the per-customer 360 view. Authentication is user id + password against
    /// ADM_BankLogin, held in its own session slot (see BankAdminAuthorize).
    /// </summary>
    public class BankAdminController : Controller
    {
        private readonly IBankAdminService _bankAdmin;
        private readonly IBlobAnalysisService _analysis;
        private readonly ILogger<BankAdminController> _logger;

        public BankAdminController(IBankAdminService bankAdmin, IBlobAnalysisService analysis,
            ILogger<BankAdminController> logger)
        {
            _bankAdmin = bankAdmin;
            _analysis = analysis;
            _logger = logger;
        }

        /// <summary>Bank login screen. Already signed in → straight to the dashboard.</summary>
        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetCurrentBankUser() != null)
                return RedirectToAction(nameof(Dashboard));

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string userId, string password, CancellationToken ct)
        {
            var result = await _bankAdmin.ValidateLoginAsync(userId, password, ct);

            if (result.Result != tflResultType.tflUserAuthenticated || result.Data == null)
            {
                _logger.LogWarning("Bank login failed for {UserId}: {Message}", userId, result.Message);
                return Json(new { status = false, message = result.Message });
            }

            result.Data.ClientIP = await IP_Get_Service.GetClientIPAddressAsync(HttpContext);
            HttpContext.Session.SetCurrentBankUser(result.Data);

            _logger.LogInformation("Bank user {UserId} signed in from {Ip}", result.Data.UserId, result.Data.ClientIP);

            return Json(new
            {
                status = true,
                message = "Login successful.",
                redirectUrl = Url.Action(nameof(Dashboard), "BankAdmin")
            });
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Remove(SessionKeys.CurrentBankUser);
            return RedirectToAction(nameof(Login));
        }

        /// <summary>Portfolio dashboard — KPIs and charts across all onboarded MSMEs.</summary>
        [BankAdminAuthorize]
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

        /// <summary>
        /// Backfills t_MsmeScoreSummary from the blob result.json of every onboarded
        /// UAN. Needed for MSMEs scored before the summary table existed; after that,
        /// AnalyzeAsync keeps the table current on its own.
        /// <para>
        /// The loop lives here rather than in BankAdminService because
        /// BlobAnalysisService already depends on IBankAdminService — calling back the
        /// other way would be a circular registration. The controller is the one place
        /// that legitimately holds both.
        /// </para>
        /// </summary>
        [BankAdminAuthorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResyncScores(CancellationToken ct)
        {
            int synced = 0, skipped = 0, failed = 0;

            try
            {
                var uans = await _bankAdmin.GetAllUansAsync(ct);

                foreach (var uan in uans)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var result = await _analysis.GetResultAsync(uan, ct);
                        if (result == null) { skipped++; continue; }

                        await _bankAdmin.SaveScoreSummaryAsync(uan, result, ct);
                        synced++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        failed++;
                        _logger.LogError(ex, "Resync: failed for {Uan}", uan);
                    }
                }

                _logger.LogInformation("Resync complete: {Synced} synced, {Skipped} unscored, {Failed} failed.",
                    synced, skipped, failed);

                return Json(new
                {
                    status = true,
                    message = $"Synced {synced} score(s). {skipped} not yet analysed"
                              + (failed > 0 ? $", {failed} failed" : "") + "."
                });
            }
            catch (OperationCanceledException)
            {
                return Json(new { status = false, message = "Sync was cancelled before it finished." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Score resync failed");
                return Json(new { status = false, message = "Score sync failed. Check the logs for details." });
            }
        }

        /// <summary>Searchable, filterable list of every onboarded MSME.</summary>
        [BankAdminAuthorize]
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
        /// Customer 360 — profile plus the full Financial Health Card for one MSME.
        /// The UAN comes from the route, guarded by BankAdminAuthorize; the customer
        /// pages remain session-scoped so an MSME can still only see its own data.
        /// </summary>
        [BankAdminAuthorize]
        [HttpGet]
        public async Task<IActionResult> Customer(string uan, CancellationToken ct)
        {
            var model = new BankCustomerDetailViewModel { Uan = uan?.Trim() };

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
