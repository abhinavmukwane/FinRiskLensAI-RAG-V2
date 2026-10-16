using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Cases
{
    public enum CreditOutcome { Approve, Refer, Decline }

    public enum LoanProduct { WorkingCapital, TermLoan }

    /// <summary>
    /// The AI credit decision for a customer whose verdict is Clear (Doc/12_CUSTOMER_RISK_LENS.md §5). Decided within
    /// the code-computed credit capacity, every figure traced to it, every policy point cited — then accepted by
    /// the officer, who may lower the amount but never raise it. Append-only: the latest row is the decision.
    /// </summary>
    public class CreditDecision : AuditableEntity
    {
        public int CreditDecisionID { get; set; }
        public int LoanApplicationID { get; set; }
        public string Uan { get; set; } = string.Empty;
        public LoanProduct Product { get; set; }
        public decimal RequestedAmount { get; set; }
        public int TenureMonths { get; set; }
        public decimal RatePct { get; set; }
        /// <summary>JSON of the CreditCapacity it was decided within.</summary>
        public string Capacity { get; set; } = "{}";
        public CreditOutcome Outcome { get; set; }
        public decimal RecommendedAmount { get; set; }
        /// <summary>JSON: [{ "text": …, "sources": [policy breadcrumb, …] }].</summary>
        public string Reasons { get; set; } = "[]";
        /// <summary>JSON: [condition, …].</summary>
        public string Conditions { get; set; } = "[]";
        /// <summary>JSON: [{ "text": …, "why": … }] — reasons the grounding check removed.</summary>
        public string Dropped { get; set; } = "[]";
        public string Model { get; set; } = string.Empty;
        public decimal? AcceptedAmount { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public string? AcceptedBy { get; set; }
    }

    public sealed record DecisionReason(string Text, IReadOnlyList<string> Sources);
    public sealed record DroppedReason(string Text, string Why);
}
