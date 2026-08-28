using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Models;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// The printable Financial Health Report — opened from the "Download Report"
    /// button on the Financial Health Card. Renders the logged-in MSME's own
    /// current score; never takes a UAN from the request, so a customer can only
    /// ever print their own report.
    /// </summary>
    [CustDashboardAuthorize]
    public class ReportController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly IBlobAnalysisService _analysis;
        private readonly IMsmeDataStore _store;
        private readonly CustomerProfileBuilder _profile;

        public ReportController(IEmailService emailService, IBlobAnalysisService analysis,
            IMsmeDataStore store, CustomerProfileBuilder profile)
        {
            _emailService = emailService;
            _analysis = analysis;
            _store = store;
            _profile = profile;
        }

        [HttpGet]
        public async Task<IActionResult> FinancialReport(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            var model = new FinancialReportViewModel { Uan = uan };

            if (string.IsNullOrWhiteSpace(uan))
            {
                model.LoadError = "No Udyam number is linked to your account yet, so there is no report to generate.";
                return View(model);
            }

            try
            {
                model.Status = await _analysis.GetStatusAsync(uan, ct);
                model.Result = await _analysis.GetResultAsync(uan, ct);
                model.Udyam = await _profile.GetUdyamAsync(uan);
                model.EnterpriseName = model.Udyam?.HasData == true ? model.Udyam.EnterpriseName : null;

                if (model.Result == null)
                    model.LoadError = "No score has been computed yet for this account — run the analysis from the Financial Health Card first.";
            }
            catch (OperationCanceledException)
            {
                model.LoadError = "The report is still being prepared while the service warms up. Please refresh in a few seconds.";
            }
            catch (Exception)
            {
                model.LoadError = "Could not load the report right now. Please try again later.";
            }

            return View(model);
        }
    }
}
