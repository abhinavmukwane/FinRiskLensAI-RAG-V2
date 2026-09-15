using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Core.Interfaces.IServices.LoanCase
{
    /// <summary>
    /// Shapes one channel's outbound payload. LOS, ULI and ONDC are three genuinely
    /// different schemas — a bank-internal appraisal case, an OCEN-lineage loan
    /// application, and a beckn envelope — so each gets its own builder behind this
    /// one contract. Adding a co-lender or GeM later is one more class.
    /// </summary>
    public interface ILoanCasePayloadBuilder
    {
        LoanCaseChannel Channel { get; }

        /// <summary>
        /// Builds the payload as a plain object graph; the service serialises it so
        /// preview and push are guaranteed to show identical bytes.
        /// </summary>
        object Build(BankCustomerRow customer, RiskAnalysisResult result, LoanCaseContext context);
    }

    /// <summary>Per-push facts that are not part of the customer or the score.</summary>
    public class LoanCaseContext
    {
        public string CaseReference { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string LenderId { get; set; } = string.Empty;
        public string LenderName { get; set; } = string.Empty;
        public string OndcSubscriberId { get; set; } = string.Empty;
        public string OndcSubscriberUri { get; set; } = string.Empty;
        public string RequestedByUserId { get; set; } = string.Empty;
        public string? RequestedByName { get; set; }
        public string? BranchIfsc { get; set; }
        public string? BranchName { get; set; }
        /// <summary>Which raw sources were present in the MSME's data folder.</summary>
        public List<string> DataSources { get; set; } = new();
    }
}
