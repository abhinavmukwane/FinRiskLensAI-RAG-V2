using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Checks;

namespace FinRiskLensAI.Tests
{
    public class SourceChecksTests
    {
        private static AaRiskFlag Flag(string severity, string title) => new() { Severity = severity, Title = title, Evidence = "e", CreditRelevance = "r" };

        [Fact]
        public void Bank_flags_become_one_finding_per_pattern_and_positives_are_skipped()
        {
            var bank = new AaAnalysisResult
            {
                HasData = true,
                RiskFlags =
                {
                    Flag("High", "High potential pass-through behaviour"),
                    Flag("Medium", "Potential pass-through behaviour observed"),   // same pattern, less severe
                    Flag("High", "Banking discipline events identified"),
                    Flag("Medium", "Irregular banking activity"),                  // not in the table: typed from its title
                    Flag("Positive", "Positive net cash flow")
                }
            };

            var findings = SourceChecks.BankFlags(bank, 7);

            Assert.Equal(new[] { "AA_DISCIPLINE_EVENTS", "AA_IRREGULAR_BANKING_ACTIVITY", "AA_PASS_THROUGH" },
                         findings.Select(f => f.Type).OrderBy(t => t));
            Assert.Equal(FindingSeverity.High, findings.Single(f => f.Type == "AA_PASS_THROUGH").Severity);
            Assert.All(findings, f => Assert.Equal(RiskSections.Bank, RiskSections.Of(f.Type)));
            Assert.All(findings, f => Assert.Equal(7, f.LoanApplicationID));
            Assert.Empty(SourceChecks.BankFlags(new AaAnalysisResult { HasData = false, RiskFlags = { Flag("High", "Negative balance observed") } }, 7));
        }
    }
}
