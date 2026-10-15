using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Tests
{
    public class RiskLensTests
    {
        [Fact]
        public void The_verdict_follows_open_findings_and_waits_while_assessing()
        {
            Assert.Equal(RiskVerdict.Assessing, RiskSummary.Decide(assessing: true, openHigh: 2, openOther: 0));
            Assert.Equal(RiskVerdict.HighRisk, RiskSummary.Decide(false, 1, 3));
            Assert.Equal(RiskVerdict.Review, RiskSummary.Decide(false, 0, 1));
            Assert.Equal(RiskVerdict.Clear, RiskSummary.Decide(false, 0, 0));
        }

        [Fact]
        public void Every_finding_type_lands_in_its_section()
        {
            Assert.Equal(RiskSections.Documents, RiskSections.Of("STATEMENT_VS_AA_MISMATCH"));
            Assert.Equal(RiskSections.Documents, RiskSections.Of("BALANCE_DISCONTINUITY"));
            Assert.Equal(RiskSections.Bank, RiskSections.Of("UNDECLARED_EMI"));
            Assert.Equal(RiskSections.Bank, RiskSections.Of("PASS_THROUGH"));
            Assert.Equal(RiskSections.Gst, RiskSections.Of("INCOME_MISMATCH"));
            Assert.Equal(RiskSections.Identity, RiskSections.Of("IDENTITY_MISMATCH"));
            Assert.Equal(RiskSections.Network, RiskSections.Of("FRAUD_RING"));
        }
    }
}
