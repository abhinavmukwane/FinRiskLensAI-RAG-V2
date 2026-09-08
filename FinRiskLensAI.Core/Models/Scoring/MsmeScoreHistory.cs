using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Scoring
{
    /// <summary>
    /// Append-only record of every score ever computed for an MSME — one row per
    /// analysis run, never updated, never deleted.
    /// <para>
    /// Why this exists alongside <see cref="MsmeScoreSummary"/>: the summary holds
    /// one row per UAN and is overwritten on each re-analysis, so it answers
    /// "what is this borrower worth today?" but destroys the answer to "what was
    /// it last quarter?". Scoring at origination is a point-in-time decision;
    /// monitoring a live exposure needs the series. This table is that series —
    /// it feeds the trend chart on the Financial Health Card and is the input a
    /// band-drop / early-warning rule compares against.
    /// </para>
    /// <para>
    /// Dimension scores are stored as columns rather than JSON so a monitoring
    /// query can filter on them directly (e.g. "compliance fell more than 30
    /// points since last run") without deserialising every row.
    /// </para>
    /// </summary>
    public class MsmeScoreHistory : AuditableEntity
    {
        public int MsmeScoreHistoryID { get; set; }

        /// <summary>Udyam Registration Number — the blob folder key. Indexed, NOT unique.</summary>
        public string Uan { get; set; } = string.Empty;

        // ── Headline score
        public double OverallScore { get; set; }
        /// <summary>ScoreBandType as text: Excellent / Good / Fair / AtRisk / HighRisk.</summary>
        public string ScoreBand { get; set; } = string.Empty;
        public double HeuristicScore { get; set; }
        public double MlCalibratedScore { get; set; }
        public double CashflowTrendSlope { get; set; }

        // ── The six dimensions, in doc order (weights 25/20/15/15/15/10)
        public double RevenueVitality { get; set; }
        public double CashFlowHealth { get; set; }
        public double TransactionTrust { get; set; }
        public double ComplianceQuotient { get; set; }
        public double BusinessStability { get; set; }
        public double DebtServiceability { get; set; }

        // ── Lending headlines, so the series can show eligibility moving too
        public decimal AnnualTurnover { get; set; }
        public decimal MonthlySurplus { get; set; }
        public decimal TotalIndicativeEligibility { get; set; }

        // ── Flags / provenance
        public bool IsAnomalous { get; set; }
        public int DimensionsExcludedCount { get; set; }
        public int DataFilesUsed { get; set; }
        public string? ModelVersion { get; set; }

        /// <summary>When the engine computed this score (UTC) — the series x-axis.</summary>
        public DateTime ComputedAt { get; set; }

        /// <summary>
        /// What produced this run: "analysis" for the normal pipeline, "seed" for
        /// demo backfill. Keeps fabricated points distinguishable from real ones.
        /// </summary>
        public string? Source { get; set; }
    }

    /// <summary>One point on the score trend chart.</summary>
    public class ScoreHistoryPoint
    {
        public DateTime ComputedAt { get; set; }
        public double OverallScore { get; set; }
        public string ScoreBand { get; set; } = string.Empty;
        public double RevenueVitality { get; set; }
        public double CashFlowHealth { get; set; }
        public double TransactionTrust { get; set; }
        public double ComplianceQuotient { get; set; }
        public double BusinessStability { get; set; }
        public double DebtServiceability { get; set; }
        public decimal TotalIndicativeEligibility { get; set; }
        public bool IsAnomalous { get; set; }
    }
}
