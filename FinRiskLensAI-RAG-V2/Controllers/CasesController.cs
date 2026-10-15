using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Cases;
using FinRiskLensAI.Fraud.Pipeline;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Cases inbox, intake and case view for the bank portal. Uploaded documents are
    /// applicant financials, so everything here is bank-session only.
    /// </summary>
    [BankAdminAuthorize]
    [Route("cases")]
    public class CasesController : Controller
    {

        private readonly ICaseService _cases;
        private readonly CaseQueue _queue;
        private readonly ILogger<CasesController> _logger;

        public CasesController(ICaseService cases, CaseQueue queue, ILogger<CasesController> logger)
        {
            _cases = cases;
            _queue = queue;
            _logger = logger;
        }

        [HttpGet("rings")]
        public async Task<IActionResult> Rings(CancellationToken ct)
            => View(await _cases.ListRingsAsync(ct));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id, CancellationToken ct)
        {
            var model = await _cases.GetAsync(id, ct);
            return model == null ? NotFound() : View(model);
        }

        [HttpPost("{id:int}/checks")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunChecks(int id, string? returnUrl, CancellationToken ct)
        {
            await _cases.RunChecksAsync(id, ct);
            return Back(returnUrl) ?? RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>
        /// Re-runs the checks and the Investigator in the background. The case shows Processing,
        /// and the page refreshes itself until the pipeline sets it back to Review.
        /// </summary>
        [HttpPost("{id:int}/investigate")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Investigate(int id, string? returnUrl, CancellationToken ct)
        {
            if (!await _cases.MarkProcessingAsync(id, ct)) return NotFound();
            _queue.Enqueue(id);
            return Back(returnUrl) ?? RedirectToAction(nameof(Detail), new { id });
        }

        /// <summary>Confirm, dismiss, or reopen a finding. The note travels with it into case memory later.</summary>
        [HttpPost("{id:int}/findings/{findingId:int}/review")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(int id, int findingId, ReviewStatus status, string? note, string? returnUrl,
                                                CancellationToken ct)
        {
            if (!Enum.IsDefined(status) || note?.Length > 2000) return BadRequest();

            var user = HttpContext.Session.GetCurrentBankUser()!;
            var found = await _cases.ReviewFindingAsync(id, findingId, status, user.UserId, note, ct);
            if (!found) return NotFound();
            var back = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(nameof(Detail), new { id });
            return Redirect(back + $"#finding-{findingId}");
        }

        /// <summary>Back to the page the action started from — only ever a local URL, never an open redirect.</summary>
        private IActionResult? Back(string? returnUrl) => Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl!) : null;

        /// <summary>The stored file, inline — the evidence viewer's source.</summary>
        [HttpGet("{id:int}/documents/{docId:int}")]
        public async Task<IActionResult> Document(int id, int docId, CancellationToken ct)
        {
            var file = await _cases.OpenDocumentAsync(id, docId, ct);
            if (file == null) return NotFound();

            // Applicant financials: never cached by the browser or a proxy
            Response.Headers.CacheControl = "no-store";
            return File(file.Content, file.ContentType, enableRangeProcessing: true);
        }
    }
}
