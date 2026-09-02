using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Interfaces;

namespace FinRiskLensAI.Core.Models.Scoring
{
    /// <summary>
    /// ViewModel for /Report/FinancialReport — the printable, single-page version
    /// of the Financial Health Card. Built entirely from the same sources the
    /// dashboard card and the bank portal's Customer 360 already read
    /// (IBlobAnalysisService for the score, CustomerProfileBuilder for the Udyam
    /// identity), so the report can never show a number the card doesn't.
    /// </summary>
    public class FinancialReportViewModel
    {
        public string? Uan { get; set; }
        public string? LoadError { get; set; }

        public string? EnterpriseName { get; set; }
        public UdyamDetailsViewModel? Udyam { get; set; }
        public MsmeDataStatusReport? Status { get; set; }
        public RiskAnalysisResult? Result { get; set; }

        public bool HasResult => Result != null;

        /// <summary>Stable per-report identifier, e.g. FHC-20260704-0067394 (date computed + UAN's own serial).</summary>
        public string ReportId =>
            Result != null && !string.IsNullOrWhiteSpace(Uan)
                ? $"FHC-{Result.ComputedAt:yyyyMMdd}-{Uan[(Uan.LastIndexOf('-') + 1)..]}"
                : "FHC-PENDING";
    }
}
