using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Facts;

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
        object Build(BankCustomerRow customer, SourceFacts facts, LoanCaseContext context);
    }

    /// <summary>Per-push details that are not part of the customer or the source facts.</summary>
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

        /// <summary>Risk Lens verdict and reviewed findings summary (Task T10).</summary>
        public LoanCaseRiskAssessment? RiskAssessment { get; set; }

        /// <summary>Accepted AI credit decision details (Task T10).</summary>
        public LoanCaseCreditDecision? CreditDecision { get; set; }
    }

    public class LoanCaseRiskAssessment
    {
        public RiskVerdict Verdict { get; set; }
        public DateTime? AssessedAt { get; set; }
        public List<LoanCaseFindingSummary> ReviewedFindings { get; set; } = new();
    }

    public class LoanCaseFindingSummary
    {
        public string Type { get; set; } = string.Empty;
        public FindingSeverity Severity { get; set; }
        public ReviewStatus ReviewStatus { get; set; }
    }

    public class LoanCaseCreditDecision
    {
        public string Decision { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int TenureMonths { get; set; }
        public decimal? Emi { get; set; }
        public decimal? Foir { get; set; }
        public object? Reasons { get; set; }
        public List<string> Conditions { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public string? AcceptedBy { get; set; }
    }
}
