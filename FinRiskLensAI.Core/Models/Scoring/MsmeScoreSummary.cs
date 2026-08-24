using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Scoring
{
    /// <summary>
    /// Flat SQL projection of the blob-persisted result.json, one row per UAN,
    /// upserted by the analysis service each time a score is computed.
    /// <para>
    /// Why this exists: the full RiskAnalysisResult lives only in blob storage
    /// (one JSON per MSME). The bank portal needs scores for *all* customers at
    /// once — to sort, filter and chart them — and fanning out one blob read per
    /// customer does not scale. This table is the queryable index over those
    /// results; the blob remains the source of truth for full detail.
    /// </para>
    /// </summary>
    public class MsmeScoreSummary : AuditableEntity
    {
        public int MsmeScoreSummaryID { get; set; }

        /// <summary>Udyam Registration Number — the blob folder key. Unique.</summary>
        public string Uan { get; set; } = string.Empty;

        // ── Score
        public double OverallScore { get; set; }
        /// <summary>ScoreBandType as text: Excellent / Good / Fair / AtRisk / HighRisk.</summary>
        public string ScoreBand { get; set; } = string.Empty;
        public double HeuristicScore { get; set; }
        public double MlCalibratedScore { get; set; }
        public double CashflowTrendSlope { get; set; }

        // ── Lending headline figures (null-safe: 0 when Lending was not computed)
        public decimal WorkingCapitalLimit { get; set; }
        public decimal TermLoanCapacity { get; set; }
        public decimal TotalIndicativeEligibility { get; set; }
        public decimal AnnualTurnover { get; set; }
        public decimal MonthlySurplus { get; set; }

        // ── Top recommended product (first entry of Recommendations)
        public string? TopProductName { get; set; }
        public string? TopProductScheme { get; set; }

        // ── Flags / provenance
        public bool IsAnomalous { get; set; }
        public int DimensionsExcludedCount { get; set; }
        public string? ModelVersion { get; set; }
        public DateTime ComputedAt { get; set; }
    }
}
