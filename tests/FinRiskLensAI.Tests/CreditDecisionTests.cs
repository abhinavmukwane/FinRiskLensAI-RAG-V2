using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Credit;

namespace FinRiskLensAI.Tests
{
    public class CreditDecisionTests
    {
        // ₹1,20,000 assessed income at 55% FOIR, ₹16,000 existing EMIs → ₹50,000 room; ₹15,00,000 eligible
        private static readonly CreditCapacity Capacity = new(
            BankMonthlyIncome: 125000, ItrMonthlyIncome: 120000, AssessedMonthlyIncome: 120000, MaxFoirPct: 55,
            ExistingMonthlyEmi: 16000, MaxNewMonthlyEmi: 50000, RequestedAmount: 2000000, TenureMonths: 36, AnnualRatePct: 11,
            RequestedMonthlyEmi: 65478, FoirWithRequestedPct: 67.9m, EligibleAmount: 1500000, AuditCapAmount: null,
            PolicyClauses: new[] { "§3.1.2", "§3.1.3" });

        private static readonly Dictionary<long, string> Chunks = new() { [7] = "Sample Credit Policy › §3.1 FOIR" };

        private static ProposedReason Reason(string text, params long[] citations) => new() { Text = text, Citations = citations.ToList() };

        [Fact]
        public void An_amount_above_the_maximum_is_capped_and_untraced_figures_are_removed()
        {
            var g = CreditDecisionService.Ground(new ProposedDecision
            {
                Outcome = "approve",
                RecommendedAmount = 1800000,
                Reasons = new()
                {
                    Reason("Assessed income ₹1,20,000 is the lower of bank and ITR.", 7, 99),   // 99 was never retrieved
                    Reason("Room for a new EMI of ₹50,000 supports ₹15,00,000.", 7),
                    Reason("Average balance of ₹3,40,000 shows comfort."),                        // not a capacity figure
                },
                Conditions = new() { "Margin of ₹2,00,000 brought in first.", "Stock statement every quarter." }
            }, Capacity, Chunks);

            Assert.Equal(CreditOutcome.Approve, g.Outcome);
            Assert.Equal(1500000, g.RecommendedAmount);
            Assert.Equal(2, g.Reasons.Count);
            Assert.Equal(new[] { "Sample Credit Policy › §3.1 FOIR" }, g.Reasons[0].Sources);
            Assert.Equal(new[] { "Stock statement every quarter." }, g.Conditions);
            Assert.Equal(3, g.Dropped.Count);   // the cap, the balance reason, the margin condition
        }

        [Fact]
        public void A_decision_with_nothing_to_lend_or_no_grounded_reason_is_referred()
        {
            var none = Capacity with { EligibleAmount = 0 };
            Assert.Equal(CreditOutcome.Refer, CreditDecisionService.Ground(
                new ProposedDecision { Outcome = "APPROVE", RecommendedAmount = 500000, Reasons = new() { Reason("FOIR is within the limit.", 7) } },
                none, Chunks).Outcome);

            var g = CreditDecisionService.Ground(
                new ProposedDecision { Outcome = "APPROVE", RecommendedAmount = 900000, Reasons = new() { Reason("Turnover of ₹4,00,00,000 is strong.") } },
                Capacity, Chunks);
            Assert.Equal(CreditOutcome.Refer, g.Outcome);
            Assert.Empty(g.Reasons);

            var decline = CreditDecisionService.Ground(
                new ProposedDecision { Outcome = "DECLINE", RecommendedAmount = 300000, Reasons = new() { Reason("FOIR with the request is 67.9%, above 55%.", 7) } },
                Capacity, Chunks);
            Assert.Equal((CreditOutcome.Decline, 0m), (decline.Outcome, decline.RecommendedAmount));
        }
    }
}
