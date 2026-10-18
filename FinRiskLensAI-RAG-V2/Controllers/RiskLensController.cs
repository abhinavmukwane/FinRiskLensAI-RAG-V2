using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Pipeline;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// The actions behind a customer's Risk Lens (BankAdmin/Customer, tab "risk"): re-run the checks, run the
    /// investigation, review a finding, open an uploaded document. Every action returns to the page it came
    /// from. The customer's files are financials, so everything here is bank-session only.
    /// </summary>
    [BankAdminAuthorize]
    [Route("risk-lens")]
    public class RiskLensController : Controller
    {
        private readonly ICaseService _cases;
        private readonly CaseQueue _queue;

        public RiskLensController(ICaseService cases, CaseQueue queue)
        {
            _cases = cases;
            _queue = queue;
        }

        [HttpPost("{id:int}/checks")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunChecks(int id, string? returnUrl, CancellationToken ct)
        {
            await _cases.RunChecksAsync(id, ct);
            return Redirect(Back(returnUrl));
        }

        /// <summary>
        /// Re-runs the checks and the Investigator in the background. The assessment shows Processing,
        /// and the Risk Lens refreshes itself until the pipeline sets it back to Review.
        /// </summary>
        [HttpPost("{id:int}/investigate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Investigate(int id, string? returnUrl, CancellationToken ct)
        {
            if (!await _cases.MarkProcessingAsync(id, ct)) return NotFound();
            _queue.Enqueue(id);
            return Redirect(Back(returnUrl));
        }

        /// <summary>Confirm, dismiss, or reopen a finding. Dismissing requires a written reason (policy §6.3).</summary>
        [HttpPost("{id:int}/findings/{findingId:int}/review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, int findingId, ReviewStatus status, string? note, string? returnUrl,
                                                CancellationToken ct)
        {
            if (!Enum.IsDefined(status) || note?.Length > 2000) return BadRequest();

            if (status == ReviewStatus.Dismissed && string.IsNullOrWhiteSpace(note))
            {
                TempData["Error"] = "Dismissing a finding requires a written rationale on the case file (Credit Policy §6.3).";
                return Redirect(Back(returnUrl) + $"#finding-{findingId}");
            }

            var user = HttpContext.Session.GetCurrentBankUser()!;
            if (!await _cases.ReviewFindingAsync(id, findingId, status, user.UserId, note, ct)) return NotFound();
            return Redirect(Back(returnUrl) + $"#finding-{findingId}");
        }

        /// <summary>The stored file, inline — the evidence viewer's source.</summary>
        [HttpGet("{id:int}/documents/{docId:int}")]
        public async Task<IActionResult> Document(int id, int docId, CancellationToken ct)
        {
            var file = await _cases.OpenDocumentAsync(id, docId, ct);
            if (file == null) return NotFound();

            // Customer financials: never cached by the browser or a proxy
            Response.Headers.CacheControl = "no-store";
            return File(file.Content, file.ContentType, enableRangeProcessing: true);
        }

        /// <summary>Back to the page the action started from — only ever a local URL, never an open redirect.</summary>
        private string Back(string? returnUrl)
            => Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action("Customers", "BankAdmin")!;
    }
}
