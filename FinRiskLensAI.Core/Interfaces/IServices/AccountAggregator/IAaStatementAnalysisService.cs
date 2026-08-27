using FinRiskLensAI.Core.Models.AccountAggregator;

namespace FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator
{
    /// <summary>
    /// Deterministic bank-statement deep analysis over the AA JSON already stored
    /// in blob storage. Pure computation — it does not call the AA API and does
    /// not read blob storage itself; the caller supplies the JSON.
    /// </summary>
    public interface IAaStatementAnalysisService
    {
        /// <summary>Normalise, classify and analyse a raw AA payload.</summary>
        AaAnalysisResult Analyze(string? aaJson);

        /// <summary>
        /// Compact, structured payload for the LLM — metrics, category rollups,
        /// monthly summaries, pass-through pairs and a bounded transaction sample.
        /// Never the full statement.
        /// </summary>
        string BuildAiPayload(AaAnalysisResult result);

        /// <summary>Parse and validate the model's JSON reply; never throws.</summary>
        AaAiInsights ParseAiResponse(string? rawResponse);
    }
}
