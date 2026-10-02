using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Cases;
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
        // Every file at the cap, plus room for the form fields
        private const long MaxRequestBytes = (long)CaseService.MaxFiles * CaseService.MaxFileBytes + 1024 * 1024;

        private readonly ICaseService _cases;
        private readonly ILogger<CasesController> _logger;

        public CasesController(ICaseService cases, ILogger<CasesController> logger)
        {
            _cases = cases;
            _logger = logger;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(CancellationToken ct)
            => View(await _cases.ListAsync(ct));

        [HttpGet("new")]
        public IActionResult New() => View(new NewCaseRequest());

        [HttpPost("new")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
        public async Task<IActionResult> New(NewCaseRequest request, List<IFormFile> files, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(request);

            // Check sizes before buffering, so an oversize file is refused without being read
            var tooBig = files.FirstOrDefault(f => f.Length > CaseService.MaxFileBytes);
            if (tooBig != null)
            {
                ModelState.AddModelError("", $"{tooBig.FileName} is over {CaseService.MaxFileBytes / 1024 / 1024} MB.");
                return View(request);
            }

            var uploads = new List<CaseUpload>();
            foreach (var f in files)
            {
                using var ms = new MemoryStream((int)f.Length);
                await f.CopyToAsync(ms, ct);
                uploads.Add(new CaseUpload(f.FileName, ms.ToArray()));
            }

            var user = HttpContext.Session.GetCurrentBankUser()!;
            var result = await _cases.CreateAsync(request, uploads, user.UserId, ct);
            if (result.Error != null)
            {
                ModelState.AddModelError("", result.Error);
                return View(request);
            }

            _logger.LogInformation("Case {CaseId} created by {User} from {Count} uploaded file(s)",
                result.LoanApplicationID, user.UserId, uploads.Count);
            return RedirectToAction(nameof(Detail), new { id = result.LoanApplicationID });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Detail(int id, CancellationToken ct)
        {
            var model = await _cases.GetAsync(id, ct);
            return model == null ? NotFound() : View(model);
        }

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
