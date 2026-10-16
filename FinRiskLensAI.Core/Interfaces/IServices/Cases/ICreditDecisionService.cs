using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Core.Interfaces.IServices.Cases
{
    /// <summary>What the officer asks for when generating a decision.</summary>
    public sealed record CreditRequest(LoanProduct Product, decimal RequestedAmount, int TenureMonths, decimal RatePct);

    /// <summary>
    /// The AI credit decision for a Clear customer (Doc/12_CUSTOMER_RISK_LENS.md §5; Doc/13_TEAM_WORK_PLAN.md T9, T10).
    /// Errors are returned as text for the officer, never thrown.
    /// </summary>
    public interface ICreditDecisionService
    {
        /// <summary>Decides within the customer's credit capacity, grounds the result, stores it.</summary>
        Task<(CreditDecision? Decision, string? Error)> DecideAsync(string uan, CreditRequest request, string actor, CancellationToken ct = default);

        /// <summary>The officer accepts the latest decision, at the recommended amount or lower. Null on success.</summary>
        Task<string?> AcceptAsync(string uan, int creditDecisionId, decimal amount, string actor, CancellationToken ct = default);

        /// <summary>The latest decision for the customer, or null.</summary>
        Task<CreditDecision?> GetLatestAsync(string uan, CancellationToken ct = default);

        /// <summary>Why the loan case cannot be sent yet — null when it can: the customer is Clear and the latest decision is an accepted Approve.</summary>
        Task<string?> PushBlockedAsync(string uan, CancellationToken ct = default);
    }
}
