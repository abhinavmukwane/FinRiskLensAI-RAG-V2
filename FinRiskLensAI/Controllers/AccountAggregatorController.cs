using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Account Aggregator screens for the logged-in MSME.
    /// <para>
    /// This controller does <b>not</b> call the AA API and does <b>not</b> upload to
    /// blob storage — the existing consent journey
    /// (<see cref="DashboardController.InitiateAAFetch"/> →
    /// <c>AccountAggregatorService</c> → <c>IMsmeDataStore</c>) already does that.
    /// Here the stored <c>aa.json</c> is read back through
    /// <see cref="CustomerProfileBuilder"/> and analysed.
    /// </para>
    /// The UAN always comes from the authenticated session, never from the request.
    /// </summary>
    [CustDashboardAuthorize]
    public class AccountAggregatorController : Controller
    {
        private readonly CustomerProfileBuilder _profile;
        private readonly IAaStatementAnalysisService _analysis;
        private readonly IChatbotService _chatbot;
        private readonly ILogger<AccountAggregatorController> _logger;

        public AccountAggregatorController(
            CustomerProfileBuilder profile,
            IAaStatementAnalysisService analysis,
            IChatbotService chatbot,
            ILogger<AccountAggregatorController> logger)
        {
            _profile = profile;
            _analysis = analysis;
            _chatbot = chatbot;
            _logger = logger;
        }

        /// <summary>Account Aggregator detail — linked accounts plus the deep-analysis entry point.</summary>
        [HttpGet]
        public async Task<IActionResult> AADetails(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetAaAnalysisAsync(uan, ct));
        }

        /// <summary>Bank Statement Deep Analysis — the full credit-analyst view.</summary>
        [HttpGet]
        public async Task<IActionResult> DeepAnalysis(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetAaAnalysisAsync(uan, ct));
        }

        /// <summary>
        /// Section 18 — AI deep insights. Called on demand from the analysis page so
        /// the deterministic report is never blocked by the model. Reuses the existing
        /// FinRiskAI chat service; only computed metrics and bounded samples are sent.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AiInsights(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();

            var result = await _profile.GetAaAnalysisAsync(uan, ct);
            if (!result.HasData)
                return Json(new { status = false, message = result.LoadError ?? "No statement available to analyse." });

            try
            {
                var payload = _analysis.BuildAiPayload(result);
                var raw = await _chatbot.AskAsync(payload, null, AnalystInstruction, ct);
                var ai = _analysis.ParseAiResponse(raw);

                return ai.Available
                    ? Json(new { status = true, data = ai })
                    : Json(new { status = false, message = ai.Error ?? "The AI assessment could not be generated." });
            }
            catch (OperationCanceledException)
            {
                return Json(new { status = false, message = "The AI assessment timed out. The deterministic analysis above remains valid." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AA AI insight generation failed for {Uan}", uan);
                return Json(new { status = false, message = "The AI assessment is unavailable right now. The deterministic analysis above remains valid." });
            }
        }

        /// <summary>
        /// Credit-analyst instruction. The supplied payload is already computed from
        /// the statement, so the model is explicitly told to reason only from it.
        /// </summary>
        private const string AnalystInstruction = @"
You are an expert Bank Statement Credit Risk Analyst supporting an MSME / Corporate Loan Origination System.
Analyse ONLY the structured statement metrics supplied in the screen data. They were computed from the applicant's actual bank statement.

RULES
- Never assume Total Credits = Business Turnover. Credits may include self transfers, loans, investments, refunds, interest, salary and unknown items.
- Use 'Observed' for facts directly supported by the supplied data, 'Potential' for inferred classifications, and 'Unable to determine' where data is insufficient.
- Never invent transactions, turnover, GST data or financial statements. Preserve the exact amounts and dates given.
- Never call anything fraud. Say 'potential pass-through behaviour' or 'potential round-trip behaviour'.
- Reference concrete evidence (amounts, dates, counts) from the supplied metrics when justifying a conclusion.
- Do not state loan approval or rejection decisions.

OUTPUT
Return VALID JSON ONLY — no markdown, no code fences, no commentary — exactly this shape:
{
  ""overallAssessment"": """",
  ""riskLevel"": ""Low|Moderate|High"",
  ""keyStrengths"": [],
  ""keyConcerns"": [],
  ""cashFlowAssessment"": """",
  ""inflowAssessment"": """",
  ""outflowAssessment"": """",
  ""passThroughAssessment"": """",
  ""obligationAssessment"": """",
  ""balanceAssessment"": """",
  ""bankingDisciplineAssessment"": """",
  ""creditAnalystSummary"": """",
  ""recommendedVerification"": []
}";
    }
}
