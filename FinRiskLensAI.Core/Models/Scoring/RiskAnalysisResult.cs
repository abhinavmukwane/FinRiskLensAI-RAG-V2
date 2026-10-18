using FinRiskLensAI.Core.Models.Facts;

namespace FinRiskLensAI.Core.Models.Scoring
{
    public enum ScoreBandType { Excellent, Good, Fair, AtRisk, HighRisk }

    public enum ImpactDirection { Positive, Negative }

    public class DimensionScore
    {
        public string Dimension { get; set; } = string.Empty;
        /// <summary>Display-ready sub-score: 0 to the dimension's max points (e.g. 0-250 for Revenue Vitality).</summary>
        public double Score { get; set; }
        public double MaxPoints { get; set; }
        /// <summary>Effective weight after missing-source redistribution, 0..1.</summary>
        public double EffectiveWeight { get; set; }
        public bool DataAvailable { get; set; }
        public bool UsedNeutralDefault { get; set; }
    }

    public class ScoreExplanationItem
    {
        public string Dimension { get; set; } = string.Empty;
        public string ExplanationText { get; set; } = string.Empty;
        public ImpactDirection Impact { get; set; }
        /// <summary>Relative contribution for sorting strengths/risks, 0..1.</summary>
        public double RelativeWeight { get; set; }
    }

    public class ProductRecommendationItem
    {
        public string ProductName { get; set; } = string.Empty;
        public string SchemeCode { get; set; } = string.Empty;
        public decimal IndicativeAmountMin { get; set; }
        public decimal IndicativeAmountMax { get; set; }
        public string RecommendationReason { get; set; } = string.Empty;
    }

    public class AnomalyCheckResult
    {
        public bool IsAnomalous { get; set; }
        public double AnomalyScore { get; set; }
        /// <summary>Data-integrity note, kept separate from creditworthiness per the scoring doc.</summary>
        public string? Note { get; set; }
        public double GstMonthlyTurnover { get; set; }
        public double ItrMonthlyIncome { get; set; }
        public double BankMonthlyCredits { get; set; }
    }

    public class RiskAnalysisResult
    {
        public double OverallScore { get; set; }                 // 0-1000
        public ScoreBandType ScoreBand { get; set; }
        public List<DimensionScore> Dimensions { get; set; } = new();
        public List<ScoreExplanationItem> Explanations { get; set; } = new();
        public List<ProductRecommendationItem> Recommendations { get; set; } = new();
        public AnomalyCheckResult Anomaly { get; set; } = new();
        /// <summary>Bank-decision ratios + indicative loan eligibility (null on results computed before this feature).</summary>
        public LendingAssessment? Lending { get; set; }
        /// <summary>Underwriting deep-dives.</summary>
        public BankStatementAnalysis? BankAnalysis { get; set; }
        public GstDeepDive? GstAnalysis { get; set; }
        public FinancialRatios? Financials { get; set; }
        public IndustryRiskInfo? Industry { get; set; }
        public List<string> ExcludedDimensions { get; set; } = new();
        public string ModelVersion { get; set; } = string.Empty;
        public DateTime ComputedAt { get; set; }
        /// <summary>Trend of monthly bank inflows from the SSA/regression analyzer (-1..1).</summary>
        public double CashflowTrendSlope { get; set; }
        public double HeuristicScore { get; set; }               // rules-engine component (0-1000)
        public double MlCalibratedScore { get; set; }            // calibrated component (0-1000)
    }
}
