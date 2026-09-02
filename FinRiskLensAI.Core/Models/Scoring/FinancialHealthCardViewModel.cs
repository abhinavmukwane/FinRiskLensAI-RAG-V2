using FinRiskLensAI.Core.Interfaces;

namespace FinRiskLensAI.Core.Models.Scoring
{
    public class FinancialHealthCardViewModel
    {
        public string? Uan { get; set; }
        public RiskAnalysisResult? Result { get; set; }
        public MsmeDataStatusReport? Status { get; set; }
        public string? EnterpriseName { get; set; }
        public string? LoadError { get; set; }

        public bool HasResult => Result != null;
    }
}
