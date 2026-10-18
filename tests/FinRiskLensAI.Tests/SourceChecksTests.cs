using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Facts;
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

        // ── Task T3: Turnover cross-check (GST vs ITR vs bank credits) ───────────────────

        [Fact]
        public void Turnover_consistent_figures_returns_null()
        {
            var facts = new MsmeFeatureSet
            {
                HasGst = true,
                GstMonthlyAvgTurnover = 600000,
                HasItr = true,
                ItrBusinessTurnover = 7200000, // 600,000 / month
                HasAa = true,
                AaMonthlyAvgBankCredit = 650000 // within 1.5x of 600,000
            };

            var finding = SourceChecks.Turnover(facts, new FraudOptions(), 101);
            Assert.Null(finding);
        }

        [Fact]
        public void Turnover_bank_three_times_gst_returns_high_finding()
        {
            var facts = new MsmeFeatureSet
            {
                HasGst = true,
                GstMonthlyAvgTurnover = 610000,
                HasItr = true,
                ItrBusinessTurnover = 590000 * 12,
                HasAa = true,
                AaMonthlyAvgBankCredit = 1840000 // ~3x tax filings
            };

            var finding = SourceChecks.Turnover(facts, new FraudOptions(), 101);

            Assert.NotNull(finding);
            Assert.Equal("TURNOVER_MISMATCH", finding!.Type);
            Assert.Equal(FindingSeverity.High, finding.Severity);
            Assert.Equal(FindingCategory.IncomeInflation, finding.Category);
            Assert.Contains("three times what the tax filings report", finding.Explanation);
            Assert.Equal(RiskSections.Gst, RiskSections.Of(finding.Type));
            Assert.Equal(3, finding.Evidence.Count);
        }

        [Fact]
        public void Turnover_gst_vs_itr_40_percent_apart_returns_medium_finding()
        {
            var facts = new MsmeFeatureSet
            {
                HasGst = true,
                GstMonthlyAvgTurnover = 1000000,
                HasItr = true,
                ItrBusinessTurnover = 600000 * 12, // 600,000 / month: (1,000,000 - 600,000) / 1,000,000 = 40%
                HasAa = false,
                AaMonthlyAvgBankCredit = 0
            };

            var finding = SourceChecks.Turnover(facts, new FraudOptions(), 101);

            Assert.NotNull(finding);
            Assert.Equal("TURNOVER_MISMATCH", finding!.Type);
            Assert.Equal(FindingSeverity.Medium, finding.Severity);
            Assert.Equal(FindingCategory.IncomeInflation, finding.Category);
            Assert.Contains("differ by 40.0%", finding.Explanation);
            Assert.Equal(RiskSections.Gst, RiskSections.Of(finding.Type));
        }

        [Fact]
        public void Turnover_missing_source_returns_null()
        {
            var opts = new FraudOptions();

            // Only bank credits present
            var onlyBank = new MsmeFeatureSet { HasAa = true, AaMonthlyAvgBankCredit = 1000000 };
            Assert.Null(SourceChecks.Turnover(onlyBank, opts, 101));

            // Only GST present
            var onlyGst = new MsmeFeatureSet { HasGst = true, GstMonthlyAvgTurnover = 1000000 };
            Assert.Null(SourceChecks.Turnover(onlyGst, opts, 101));

            // Only ITR present
            var onlyItr = new MsmeFeatureSet { HasItr = true, ItrBusinessTurnover = 12000000 };
            Assert.Null(SourceChecks.Turnover(onlyItr, opts, 101));
        }

        // ── Task T4: Identity check (Sources agree; company is active) ────────────────────

        [Theory]
        [InlineData("ACME ENTERPRISES PVT LTD", "Acme Enterprises Private Limited", true)]
        [InlineData("SHREEJI PRECISION COMPONENTS", "shreeji precision components.", true)]
        [InlineData("BHARAT STEEL CORP. LLP", "Bharat Steel Corp Limited Liability Partnership", true)]
        [InlineData("STAR INDUSTRIES LTD.", "Star Industries Limited", true)]
        [InlineData("M/s Alpha Logistics", "ALPHA LOGISTICS", true)]
        [InlineData("Sunrise Technologies Pvt Ltd", "Sunset Technologies Private Limited", false)]
        public void NormalizeBusinessName_matches_variations(string name1, string name2, bool expectedMatch)
        {
            var matches = SourceChecks.BusinessNamesMatch(name1, name2);
            Assert.Equal(expectedMatch, matches);
        }

        [Fact]
        public void Identity_pan_differs_between_udyam_and_itr_returns_high()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                ItrPan = "XYZWE9876K",
                UdyamName = "Apex Enterprise",
                ItrName = "Apex Enterprise",
                McaStatus = "Active"
            };

            var findings = SourceChecks.Identity(data, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("IDENTITY_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains("PAN differs between Udyam and ITR", f.Title);
            Assert.Equal(RiskSections.Identity, RiskSections.Of(f.Type));
        }

        [Fact]
        public void Identity_pan_differs_between_udyam_and_gst_returns_high()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                GstPan = "XYZWE9876K",
                UdyamName = "Apex Enterprise",
                GstName = "Apex Enterprise",
                McaStatus = "Active"
            };

            var findings = SourceChecks.Identity(data, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("IDENTITY_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains("PAN differs between Udyam and GST", f.Title);
        }

        [Fact]
        public void Identity_gstin_differs_between_udyam_and_gst_returns_high()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                GstPan = "ABCDE1234F",
                UdyamGstin = "27ABCDE1234F1Z5",
                GstGstin = "27ABCDE1234F2Z8",
                UdyamName = "Apex Enterprise",
                GstName = "Apex Enterprise",
                McaStatus = "Active"
            };

            var findings = SourceChecks.Identity(data, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("IDENTITY_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains("GSTIN differs between Udyam and GST", f.Title);
        }

        [Fact]
        public void Identity_business_name_differs_returns_medium()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                GstPan = "ABCDE1234F",
                UdyamGstin = "27ABCDE1234F1Z5",
                GstGstin = "27ABCDE1234F1Z5",
                UdyamName = "Apex Tools Private Limited",
                GstName = "Zenith Engineering Private Limited",
                McaStatus = "Active"
            };

            var findings = SourceChecks.Identity(data, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("IDENTITY_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.Medium, f.Severity);
            Assert.Contains("business name differs", f.Title);
        }

        [Fact]
        public void Identity_mca_inactive_status_returns_company_inactive_high()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                GstPan = "ABCDE1234F",
                UdyamGstin = "27ABCDE1234F1Z5",
                GstGstin = "27ABCDE1234F1Z5",
                UdyamName = "Apex Tools Private Limited",
                GstName = "Apex Tools Private Limited",
                McaStatus = "Strike Off"
            };

            var findings = SourceChecks.Identity(data, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("COMPANY_INACTIVE", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains("MCA corporate status is Strike Off", f.Title);
            Assert.Equal(RiskSections.Identity, RiskSections.Of(f.Type));
        }

        [Fact]
        public void Identity_consistent_profiles_returns_no_findings()
        {
            var data = new IdentityFacts
            {
                UdyamPan = "ABCDE1234F",
                ItrPan = "ABCDE1234F",
                GstPan = "ABCDE1234F",
                UdyamGstin = "27ABCDE1234F1Z5",
                GstGstin = "27ABCDE1234F1Z5",
                UdyamName = "Apex Tools Private Limited",
                GstName = "APEX TOOLS PVT. LTD.",
                ItrName = "Apex Tools Pvt Ltd",
                McaName = "Apex Tools Private Limited",
                McaStatus = "Active"
            };

            var findings = SourceChecks.Identity(data, 101);
            Assert.Empty(findings);
        }
    }
}
