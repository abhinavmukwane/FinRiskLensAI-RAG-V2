namespace FinRiskLensAI.Core.Models.Facts
{
    /// <summary>
    /// What the verification sources — Udyam, GST, ITR, Account Aggregator — say about
    /// one MSME: parsed and derived, never scored. Built on demand from the raw API
    /// payloads, so it is never stale.
    /// <para>
    /// <see cref="Features"/> is the complete fact base the fraud checks read; the four
    /// deep-dives are presentation-ready subsets of it, used by the loan-case payloads.
    /// </para>
    /// </summary>
    public class SourceFacts
    {
        public MsmeFeatureSet Features { get; set; } = new();

        public BankStatementAnalysis? BankAnalysis { get; set; }
        public GstDeepDive? GstAnalysis { get; set; }
        public FinancialRatios? Financials { get; set; }
        public IndustryRiskInfo? Industry { get; set; }

        public string Version { get; set; } = string.Empty;
        public DateTime ComputedAt { get; set; }
    }
}
