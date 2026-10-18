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
        /// T6 (Doc/13_TEAM_WORK_PLAN.md): pure, no Gemini, no database. Rupee figures rounded to whole rupees.
        /// <paramref name="bank"/> is null when no Account Aggregator data was shared — then bank income is zero,
        /// and so is assessed income (the lower of bank and ITR under §3.1.2).
        /// </summary>
        public static CreditCapacity Compute(MsmeFeatureSet facts, AaAnalysisResult? bank,
                                             decimal requestedAmount, int tenureMonths, decimal annualRatePct)
        {
            // 1. Bank monthly income: AA sustainable inflow ÷ months covered (§3.1.2)
            decimal bankMonthlyIncome = bank != null && bank.MonthsCovered > 0 && bank.EstimatedSustainableInflow > 0
                ? Math.Round(bank.EstimatedSustainableInflow / bank.MonthsCovered)
                : 0m;

            // 2. ITR monthly income: gross total income ÷ 12 (§3.1.2)
            decimal itrMonthlyIncome = facts.HasItr && facts.ItrLatestIncome > 0
                ? Math.Round((decimal)facts.ItrLatestIncome / 12m)
                : 0m;

            // 3. Assessed monthly income: the lower of the two (§3.1.2)
            decimal assessedMonthlyIncome = Math.Min(bankMonthlyIncome, itrMonthlyIncome);
            if (assessedMonthlyIncome < 0m) assessedMonthlyIncome = 0m;

            // 4. Maximum FOIR by assessed income (§3.1.3):
            // Up to ₹50,000 -> 45%; ₹50,001 to ₹2,00,000 -> 55%; above ₹2,00,000 -> 65%
            decimal maxFoirPct = assessedMonthlyIncome switch
            {
                <= 50_000m => 45m,
                <= 200_000m => 55m,
                _ => 65m
            };

            // 5. Existing EMIs: from AA data (§3.1.4)
            decimal existingMonthlyEmi = Math.Max(0m, Math.Round((decimal)facts.AaEmiMonthlyOutflow));

            // 6. Room for new EMI: Max FOIR × income − existing EMIs, never below zero
            decimal maxTotalObligation = Math.Round((maxFoirPct / 100m) * assessedMonthlyIncome);
            decimal maxNewMonthlyEmi = Math.Max(0m, maxTotalObligation - existingMonthlyEmi);

            // 7. Requested EMI
            decimal requestedMonthlyEmi = 0m;
            if (requestedAmount > 0m && tenureMonths > 0)
            {
                if (annualRatePct > 0m)
                {
                    double r = (double)annualRatePct / 1200.0;
                    double pow = Math.Pow(1.0 + r, tenureMonths);
                    double emi = (double)requestedAmount * (r * pow) / (pow - 1.0);
                    requestedMonthlyEmi = Math.Round((decimal)emi);
                }
                else
                {
                    requestedMonthlyEmi = Math.Round(requestedAmount / tenureMonths);
                }
            }

            // 8. FOIR with requested loan
            decimal foirWithRequestedPct = assessedMonthlyIncome > 0m
                ? Math.Round((existingMonthlyEmi + requestedMonthlyEmi) / assessedMonthlyIncome * 100m, 1)
                : (existingMonthlyEmi + requestedMonthlyEmi > 0m ? 100m : 0m);

            // 9. Eligible limit: loan that MaxNewMonthlyEmi repays over tenure at the rate
            decimal eligibleAmount = 0m;
            if (maxNewMonthlyEmi > 0m && tenureMonths > 0)
            {
                if (annualRatePct > 0m)
                {
                    double r = (double)annualRatePct / 1200.0;
                    double pow = Math.Pow(1.0 + r, tenureMonths);
                    double loan = (double)maxNewMonthlyEmi * (pow - 1.0) / (r * pow);
                    eligibleAmount = Math.Max(0m, Math.Round((decimal)loan));
                }
                else
                {
                    eligibleAmount = maxNewMonthlyEmi * tenureMonths;
                }
            }

            // 10. Audit cap: ₹50,00,000 when 44AB-liable without a tax audit report (§5.3.2)
            decimal? auditCapAmount = facts.ItrAuditApplicable && !facts.ItrAuditCompleted
                ? 5_000_000m
                : null;

            // 11. Applied policy clauses
            var clauses = new List<string> { "§3.1.2", "§3.1.3" };
            if (existingMonthlyEmi > 0m) clauses.Add("§3.1.4");
            if (auditCapAmount != null) clauses.Add("§5.3.2");

            return new CreditCapacity(
                BankMonthlyIncome: bankMonthlyIncome,
                ItrMonthlyIncome: itrMonthlyIncome,
                AssessedMonthlyIncome: assessedMonthlyIncome,
                MaxFoirPct: maxFoirPct,
                ExistingMonthlyEmi: existingMonthlyEmi,
                MaxNewMonthlyEmi: maxNewMonthlyEmi,
                RequestedAmount: requestedAmount,
                TenureMonths: tenureMonths,
                AnnualRatePct: annualRatePct,
                RequestedMonthlyEmi: requestedMonthlyEmi,
                FoirWithRequestedPct: foirWithRequestedPct,
                EligibleAmount: eligibleAmount,
                AuditCapAmount: auditCapAmount,
                PolicyClauses: clauses
            );
        }
    }
}
