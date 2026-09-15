using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;

namespace FinRiskLensAI.Core.Interfaces.IServices.LoanCase
{
    /// <summary>
    /// Raises a scored MSME as a loan case in LOS / ULI / ONDC and keeps the
    /// append-only record of every attempt.
    /// </summary>
    public interface ILoanCaseService
    {
        /// <summary>
        /// The exact JSON that a push for this channel would send right now — a pure
        /// read with no side effects, for the preview tab.
        /// </summary>
        Task<LoanCasePreview> PreviewAsync(string uan, LoanCaseChannel channel, BankUserSessionModel user,
                                           CancellationToken ct = default);

        /// <summary>Builds, sends (or simulates), and records the push.</summary>
        Task<LoanCasePushResult> PushAsync(string uan, LoanCaseChannel channel, BankUserSessionModel user,
                                           string? clientIp, CancellationToken ct = default);

        Task<LoanCasePush?> GetPushAsync(int loanCasePushId, CancellationToken ct = default);

        Task<IReadOnlyList<LoanCasePushSummary>> GetStatusAsync(string uan, CancellationToken ct = default);

        /// <summary>True when an endpoint is configured for the channel (Sent vs Simulated).</summary>
        bool IsLive(LoanCaseChannel channel);
    }

    public class LoanCasePreview
    {
        public string Uan { get; set; } = string.Empty;
        public LoanCaseChannel Channel { get; set; }
        public string? EnterpriseName { get; set; }
        public string PayloadJson { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
        public bool IsLive { get; set; }
        public double Score { get; set; }
        public string Band { get; set; } = string.Empty;
        public decimal Eligibility { get; set; }
        public string? Error { get; set; }
    }
}
