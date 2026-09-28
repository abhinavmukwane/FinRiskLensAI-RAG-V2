using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Admin;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Serves the append-only score series behind the "Score History" button.
    /// <para>
    /// Its own controller because the chart is shown on two surfaces that
    /// authenticate differently — the customer's Financial Health Card and the
    /// bank portal's Customer 360 — and neither of the existing controllers can
    /// serve both: each carries a class-level filter for one session type.
    /// </para>
    /// </summary>
    [Route("score-history")]
    public class ScoreHistoryController : Controller
    {
        private readonly IBankAdminService _bankAdmin;

        public ScoreHistoryController(IBankAdminService bankAdmin) => _bankAdmin = bankAdmin;

        /// <param name="uan">
        /// Honoured only for a bank-portal session. Without one it is ignored and the
        /// caller is pinned to its own UAN, so a customer cannot read another MSME.
        /// </param>
        [HttpGet]
        public async Task<IActionResult> Series(string? uan, CancellationToken ct, int take = 50)
        {
            // Both sessions share one cookie under different keys, so a browser can
            // hold a customer AND a bank session at once. A supplied UAN wins, but
            // only for a bank session; without one it is ignored and the caller is
            // pinned to its own UAN.
            var customerUan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            var isBankUser = HttpContext.Session.GetCurrentBankUser() != null;

            var target = isBankUser && !string.IsNullOrWhiteSpace(uan) ? uan.Trim() : customerUan;

            if (string.IsNullOrWhiteSpace(target))
                return Json(new { status = false, message = "Not signed in.", points = Array.Empty<object>() });

            var points = await _bankAdmin.GetScoreHistoryAsync(target, take, ct);

            return Json(new
            {
                status = true,
                uan = target,
                count = points.Count,
                points = points.Select(p => new
                {
                    at = p.ComputedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    label = p.ComputedAt.ToString("dd MMM yy"),
                    score = Math.Round(p.OverallScore),
                    band = p.ScoreBand,
                    revenue = Math.Round(p.RevenueVitality, 1),
                    cashflow = Math.Round(p.CashFlowHealth, 1),
                    trust = Math.Round(p.TransactionTrust, 1),
                    compliance = Math.Round(p.ComplianceQuotient, 1),
                    stability = Math.Round(p.BusinessStability, 1),
                    debt = Math.Round(p.DebtServiceability, 1),
                    eligibility = p.TotalIndicativeEligibility,
                    anomalous = p.IsAnomalous
                })
            });
        }
    }
}
