using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Fraud.Credit;

namespace FinRiskLensAI.Tests
{
    public class CreditCapacityTests
    {
        private static AaAnalysisResult Bank(decimal sustainableInflow, int monthsCovered = 12) => new()
        {
            EstimatedSustainableInflow = sustainableInflow,
            MonthsCovered = monthsCovered
        };

        [Fact]
        public void Band1_Income_Up_To_50000_Gets_45_Percent_FOIR()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 480_000, // ₹40,000 / month
                AaEmiMonthlyOutflow = 5_000
            };
            var bank = Bank(sustainableInflow: 600_000, monthsCovered: 12); // ₹50,000 / month

            var capacity = CreditCapacityCalculator.Compute(facts, bank, requestedAmount: 500_000, tenureMonths: 24, annualRatePct: 12);

            Assert.Equal(40_000m, capacity.AssessedMonthlyIncome); // lower of 50k and 40k
            Assert.Equal(45m, capacity.MaxFoirPct); // Band 1
            Assert.Equal(5_000m, capacity.ExistingMonthlyEmi);
            // 45% of 40,000 = 18,000. 18,000 - 5,000 = 13,000
            Assert.Equal(13_000m, capacity.MaxNewMonthlyEmi);
            Assert.True(capacity.EligibleAmount > 0);
            Assert.Null(capacity.AuditCapAmount);
            Assert.Contains("§3.1.2", capacity.PolicyClauses);
            Assert.Contains("§3.1.3", capacity.PolicyClauses);
            Assert.Contains("§3.1.4", capacity.PolicyClauses);
        }

        [Fact]
        public void Band2_Income_50001_To_200000_Gets_55_Percent_FOIR()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 1_440_000, // ₹1,20,000 / month
                AaEmiMonthlyOutflow = 16_000
            };
            var bank = Bank(sustainableInflow: 1_500_000, monthsCovered: 12); // ₹1,25,000 / month

            var capacity = CreditCapacityCalculator.Compute(facts, bank, requestedAmount: 2_000_000, tenureMonths: 36, annualRatePct: 11);

            Assert.Equal(120_000m, capacity.AssessedMonthlyIncome);
            Assert.Equal(55m, capacity.MaxFoirPct); // Band 2
            Assert.Equal(16_000m, capacity.ExistingMonthlyEmi);
            // 55% of 120,000 = 66,000. 66,000 - 16,000 = 50,000
            Assert.Equal(50_000m, capacity.MaxNewMonthlyEmi);
            Assert.Equal(65_477m, capacity.RequestedMonthlyEmi);
            Assert.Equal(67.9m, capacity.FoirWithRequestedPct);
            Assert.Equal(1_527_244m, capacity.EligibleAmount);
            Assert.Equal(capacity.EligibleAmount, capacity.MaxLoanAmount);
        }

        [Fact]
        public void Band3_Income_Above_200000_Gets_65_Percent_FOIR()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 3_600_000, // ₹3,00,000 / month
                AaEmiMonthlyOutflow = 45_000
            };
            var bank = Bank(sustainableInflow: 4_200_000, monthsCovered: 12); // ₹3,50,000 / month

            var capacity = CreditCapacityCalculator.Compute(facts, bank, requestedAmount: 5_000_000, tenureMonths: 48, annualRatePct: 10);

            Assert.Equal(300_000m, capacity.AssessedMonthlyIncome);
            Assert.Equal(65m, capacity.MaxFoirPct); // Band 3
            Assert.Equal(45_000m, capacity.ExistingMonthlyEmi);
            // 65% of 300,000 = 195,000. 195,000 - 45,000 = 150,000
            Assert.Equal(150_000m, capacity.MaxNewMonthlyEmi);
            Assert.True(capacity.EligibleAmount > 5_000_000);
        }

        [Fact]
        public void Borrower_With_No_Room_For_New_EMI_Gets_Zero_Eligible()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 1_200_000, // ₹1,00,000 / month
                AaEmiMonthlyOutflow = 60_000  // Exceeds 55% FOIR (₹55,000)
            };
            var bank = Bank(sustainableInflow: 1_200_000, monthsCovered: 12);

            var capacity = CreditCapacityCalculator.Compute(facts, bank, requestedAmount: 1_000_000, tenureMonths: 36, annualRatePct: 12);

            Assert.Equal(100_000m, capacity.AssessedMonthlyIncome);
            Assert.Equal(55m, capacity.MaxFoirPct);
            Assert.Equal(0m, capacity.MaxNewMonthlyEmi);
            Assert.Equal(0m, capacity.EligibleAmount);
            Assert.Equal(0m, capacity.MaxLoanAmount);
        }

        [Fact]
        public void Audit_Cap_Applied_When_44AB_Liable_Without_Audit_Report()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 6_000_000, // ₹5,00,000 / month
                AaEmiMonthlyOutflow = 20_000,
                ItrAuditApplicable = true,
                ItrAuditCompleted = false // No tax audit report!
            };
            var bank = Bank(sustainableInflow: 6_000_000, monthsCovered: 12);

            var capacity = CreditCapacityCalculator.Compute(facts, bank, requestedAmount: 10_000_000, tenureMonths: 60, annualRatePct: 10);

            Assert.Equal(500_000m, capacity.AssessedMonthlyIncome);
            Assert.True(capacity.EligibleAmount > 10_000_000m);
            Assert.Equal(5_000_000m, capacity.AuditCapAmount); // ₹50 lakh cap under §5.3.2
            Assert.Equal(5_000_000m, capacity.MaxLoanAmount);  // Capped at ₹50 lakh
            Assert.Contains("§5.3.2", capacity.PolicyClauses);
        }

        [Fact]
        public void Missing_Bank_Data_Results_In_Zero_Assessed_Income()
        {
            var facts = new MsmeFeatureSet
            {
                HasItr = true,
                ItrLatestIncome = 1_200_000
            };

            var capacity = CreditCapacityCalculator.Compute(facts, bank: null, requestedAmount: 500_000, tenureMonths: 24, annualRatePct: 12);

            Assert.Equal(0m, capacity.BankMonthlyIncome);
            Assert.Equal(0m, capacity.AssessedMonthlyIncome);
            Assert.Equal(0m, capacity.MaxNewMonthlyEmi);
            Assert.Equal(0m, capacity.EligibleAmount);
            Assert.Equal(0m, capacity.MaxLoanAmount);
        }
    }
}
