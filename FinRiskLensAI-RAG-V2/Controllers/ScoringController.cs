using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    [ApiController]
    [Route("api/scoring")]
    public class ScoringController : ControllerBase
    {
        private readonly IRiskScoringService _scoring;
        private readonly ILogger<ScoringController> _logger;

        public ScoringController(IRiskScoringService scoring, ILogger<ScoringController> logger)
        {
            _scoring = scoring;
            _logger = logger;
        }

        /// <summary>
        /// Runs the full AI/ML risk analysis over the raw source payloads
        /// (Udyam, ITR, AA, GST) and returns the Financial Health Card data.
        /// </summary>
        [HttpPost("analyze")]
        public ActionResult<RiskAnalysisResult> Analyze([FromBody] RiskAnalysisRequest request)
        {
            if (request is null)
                return BadRequest("Request body is required.");

            try
            {
                var result = _scoring.Analyze(request);
                _logger.LogInformation(
                    "Risk analysis computed: score {Score} band {Band} (excluded: {Excluded})",
                    result.OverallScore, result.ScoreBand,
                    result.ExcludedDimensions.Count == 0 ? "none" : string.Join(",", result.ExcludedDimensions));
                return Ok(result);
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed source payload in risk analysis request");
                return BadRequest($"One of the source JSON payloads is malformed: {ex.Message}");
            }
        }
    }
}
