using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Facts;

namespace FinRiskLensAI.Fraud.Credit
{
    /// <summary>
    /// What the borrower can carry under the credit policy (Doc/knowledge/credit-policy.md §3, §5.3.2) — every figure
    /// the AI credit decision may quote. Computed in code, never by the model: the model decides and explains
    /// within these numbers (Doc/13_TEAM_WORK_PLAN.md T6, T9).
    /// </summary>
    public sealed record CreditCapacity(
        decimal BankMonthlyIncome,        // AA sustainable inflow ÷ months covered (§3.1.2)
        decimal ItrMonthlyIncome,         // ITR gross total income ÷ 12 (§3.1.2)
        decimal AssessedMonthlyIncome,    // the lower of the two (§3.1.2)
        decimal MaxFoirPct,               // 45 / 55 / 65 by assessed income (§3.1.3)
        decimal ExistingMonthlyEmi,       // EMIs visible in the AA data, declared or not (§3.1.4)
        decimal MaxNewMonthlyEmi,         // MaxFoirPct × income − existing EMIs, never below zero
        decimal RequestedAmount,
        int TenureMonths,
        decimal AnnualRatePct,
        decimal RequestedMonthlyEmi,      // the EMI of the requested loan
        decimal FoirWithRequestedPct,     // (existing EMIs + requested EMI) ÷ assessed income × 100
        decimal EligibleAmount,           // the loan MaxNewMonthlyEmi repays over the tenure at the rate
        decimal? AuditCapAmount,          // ₹50,00,000 when 44AB-liable without a tax audit report (§5.3.2); else null
        IReadOnlyList<string> PolicyClauses)   // the clauses applied, e.g. "§3.1.2", "§3.1.3"
    {
        /// <summary>The most the bank may lend: the eligible amount, capped by the audit rule.</summary>
        public decimal MaxLoanAmount => AuditCapAmount is decimal cap ? Math.Min(EligibleAmount, cap) : EligibleAmount;

        /// <summary>Every rupee figure here — an amount the credit decision quotes must be one of these.</summary>
        public IEnumerable<decimal> Figures => new[]
        {
            BankMonthlyIncome, ItrMonthlyIncome, AssessedMonthlyIncome, ExistingMonthlyEmi, MaxNewMonthlyEmi,
            RequestedAmount, RequestedMonthlyEmi, EligibleAmount, MaxLoanAmount
        }.Concat(AuditCapAmount is decimal a ? new[] { a } : Array.Empty<decimal>());
    }

    public static class CreditCapacityCalculator
    {
        /// <summary>
        /// T6 — to be implemented (Doc/13_TEAM_WORK_PLAN.md): pure, no Gemini, no database. Rupee figures rounded to
        /// whole rupees. <paramref name="bank"/> is null when no Account Aggregator data was shared — then the bank
        /// income is zero, and so is the assessed income (the lower of the two).
        /// </summary>
        public static CreditCapacity Compute(MsmeFeatureSet facts, AaAnalysisResult? bank,
                                             decimal requestedAmount, int tenureMonths, decimal annualRatePct)
            => throw new NotImplementedException("T6 — the credit capacity calculator is not built yet (Doc/13_TEAM_WORK_PLAN.md).");
    }
}
