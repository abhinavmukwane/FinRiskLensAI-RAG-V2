using System.Security.Cryptography;
using System.Text;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Blob-storage based data collection. Source payloads are uploaded one file at a
    /// time into the MSME's folder (named by Udyam number), so no single request ever
    /// carries the full data set. Facts are built from the folder on demand by
    /// IBlobAnalysisService.GetFactsAsync — deliberately not exposed here, because this
    /// controller has no user session and the facts include bank and tax figures.
    /// <para>
    /// Callers are upstream integrations, not people: every request must carry the shared
    /// key in <c>X-Ingest-Key</c> (config <c>MsmeData:IngestKey</c>). With no key configured
    /// the API is closed. Without this, anyone on the public URL could plant registry or bank
    /// data for any Udyam number — and those files are what uploads are verified against.
    /// </para>
    /// </summary>
    [ApiController]
    [Route("api/msme-data/{uan}")]
    public class MsmeDataController : ControllerBase, IActionFilter
    {
        private readonly IBlobAnalysisService _analysis;
        private readonly IMsmeDataStore _store;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MsmeDataController> _logger;

        public MsmeDataController(IBlobAnalysisService analysis, IMsmeDataStore store, IConfiguration configuration,
                                  ILogger<MsmeDataController> logger)
        {
            _analysis = analysis;
            _store = store;
            _configuration = configuration;
            _logger = logger;
        }

        [NonAction]
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var expected = _configuration["MsmeData:IngestKey"];
            var given = Request.Headers["X-Ingest-Key"].ToString();
            if (string.IsNullOrEmpty(expected) ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given)))
            {
                _logger.LogWarning("Rejected MSME data request without a valid ingest key from {Ip}",
                    HttpContext.Connection.RemoteIpAddress);
                context.Result = Unauthorized();
            }
        }

        [NonAction]
        public void OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>Declare which files this MSME's folder should end up containing.</summary>
        [HttpPut("manifest")]
        public async Task<IActionResult> PutManifest(string uan, [FromBody] MsmeDataManifest.ExpectedFiles expected, CancellationToken ct)
        {
            await _analysis.SaveManifestAsync(uan, expected, ct);
            return Ok(await _analysis.GetStatusAsync(uan, ct));
        }

        /// <summary>
        /// Upload one raw source payload (body = the JSON exactly as the upstream API returned it).
        /// File name must follow the convention: udyam.json, itr.json, aa.json, gst_taxpayer.json,
        /// epfo.json, gstr3b_MMyyyy.json, gstr1_summary_MMyyyy.json, gstr1_b2b_MMyyyy.json.
        /// </summary>
        [HttpPut("files/{fileName}")]
        [RequestSizeLimit(524_288_000)]   // 500 MB per file — single-file uploads, not one giant request
        public async Task<IActionResult> PutFile(string uan, string fileName, CancellationToken ct)
        {
            if (!MsmeDataFiles.IsKnown(fileName))
                return BadRequest($"Unknown file name '{fileName}'. See the naming convention in the API docs.");

            using var reader = new StreamReader(Request.Body);
            var content = await reader.ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Request body is empty — send the raw JSON payload as the body.");

            await _store.UploadAsync(uan, fileName, content, ct);
            _logger.LogInformation("Stored {File} for MSME {Uan} ({Bytes} bytes)", fileName, uan, content.Length);

            var status = await _analysis.GetStatusAsync(uan, ct);
            return Ok(new { stored = fileName, status.FilesMissing, status.ReadyForAnalysis });
        }

        /// <summary>Collection progress: what's present, what's missing, whether the folder is complete.</summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetStatus(string uan, CancellationToken ct)
            => Ok(await _analysis.GetStatusAsync(uan, ct));
    }
}
