namespace FinRiskLensAI.Core.Models.Scoring
{
    public enum RatioStatus { Strong, Adequate, Weak, NotAvailable }

    /// <summary>One bank-decision ratio with its benchmark and health status.</summary>
    public class LendingRatio
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        /// <summary>Display-ready value, e.g. "1.85x", "42%", "74 days".</summary>
        public string Display { get; set; } = string.Empty;
        /// <summary>The banking norm it's judged against, e.g. "≥ 1.5x".</summary>
        public string Benchmark { get; set; } = string.Empty;
        public RatioStatus Status { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// The loan-decision view of the analysis: the standard ratios a credit officer
    /// checks, plus indicative eligibility computed via the turnover (Nayak) method
    /// for working capital and EMI-capacity annuity for term lending, both scaled by
    /// the score band.
    /// </summary>
    public class LendingAssessment
    {
        // ── Observed financials (monthly unless stated)
        public double AnnualTurnover { get; set; }               // GST annualized
        public double MonthlySales { get; set; }
        public double MonthlyPurchases { get; set; }
        public double MonthlyBankInflow { get; set; }
        public double MonthlyBankOutflow { get; set; }
        public double MonthlySurplus { get; set; }
        public double ExistingMonthlyEmi { get; set; }

        // ── Eligibility (indicative, band-adjusted)
        /// <summary>Working capital limit via turnover method: 20% of annual turnover × band factor.</summary>
        public double WorkingCapitalLimit { get; set; }
        /// <summary>Borrower margin required under the turnover method (5% of turnover).</summary>
        public double MarginMoneyRequired { get; set; }
        /// <summary>EMI headroom after FOIR cap and observed surplus.</summary>
        public double AffordableMonthlyEmi { get; set; }
        /// <summary>Term loan supportable by the affordable EMI (5 years @ 11% p.a.).</summary>
        public double TermLoanCapacity { get; set; }
        /// <summary>Working capital + term capacity combined.</summary>
        public double TotalIndicativeEligibility { get; set; }
        /// <summary>Multiplier applied for the score band (Excellent 1.0 … High Risk 0).</summary>
        public double BandAdjustmentFactor { get; set; }

        public List<LendingRatio> Ratios { get; set; } = new();
        public List<string> Notes { get; set; } = new();
    }
}
