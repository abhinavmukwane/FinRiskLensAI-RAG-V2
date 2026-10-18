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

        // ── Task T8: Uploaded ITR vs Filed Return (ITR_MISMATCH) ─────────────────────────

        private static string SampleItrJson(string ay = "2026-27", decimal gti = 6237504, decimal turnover = 46800000,
                                            decimal totalIncome = 6212554, decimal taxPaid = 1959639)
        {
            return $$"""
            {
                "status": "SUCCESS",
                "data": {
                    "source_identifiers": { "pan": "ABCDE1234F", "gstin": "27ABCDE1234F1Z5" },
                    "filing_details": { "assessment_year": "{{ay}}", "itr_form_type": "ITR-5" },
                    "income_details": { "gross_total_income": {{gti}} },
                    "profit_and_loss_summary": { "revenue_from_operations": {{turnover}} },
                    "total_income": {{totalIncome}},
                    "tax_computation": { "taxes_paid": { "total_taxes_paid": {{taxPaid}} } }
                }
            }
            """;
        }

        private static (CaseDocument Doc, List<DocField> Fields) CreateUploadedItr(
            int docId, string ay = "2026-27", decimal gti = 6237504, decimal turnover = 46800000,
            decimal totalIncome = 6212554, decimal taxPaid = 1959639)
        {
            var doc = new CaseDocument
            {
                CaseDocumentID = docId,
                LoanApplicationID = 101,
                FileName = "uploaded_itr.pdf",
                DocType = CaseDocType.Itr
            };
            var fields = new List<DocField>
            {
                new() { DocFieldID = 1, CaseDocumentID = docId, Name = "assessment_year", Value = ay, PageNo = 1 },
                new() { DocFieldID = 2, CaseDocumentID = docId, Name = "business_turnover", Value = "₹46,800,000", NumValue = turnover, PageNo = 1 },
                new() { DocFieldID = 3, CaseDocumentID = docId, Name = "gross_total_income", Value = "₹6,237,504", NumValue = gti, PageNo = 1 },
                new() { DocFieldID = 4, CaseDocumentID = docId, Name = "total_income", Value = "₹6,212,554", NumValue = totalIncome, PageNo = 1 },
                new() { DocFieldID = 5, CaseDocumentID = docId, Name = "tax_paid", Value = "₹1,959,639", NumValue = taxPaid, PageNo = 1 }
            };
            return (doc, fields);
        }

        [Fact]
        public void ItrMismatch_matching_figures_returns_no_findings()
        {
            var (doc, fields) = CreateUploadedItr(1);
            var itrJson = SampleItrJson();

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);
            Assert.Empty(findings);
        }

        [Fact]
        public void ItrMismatch_turnover_inflated_returns_high_finding_with_field_evidence()
        {
            // Uploaded turnover ₹8,50,00,000 vs filed return ₹4,68,00,000
            var (doc, fields) = CreateUploadedItr(1, turnover: 85000000);
            var itrJson = SampleItrJson(turnover: 46800000);

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("ITR_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Equal(FindingCategory.Tampering, f.Category);
            Assert.Equal(RiskSections.Itr, RiskSections.Of(f.Type));
            Assert.Contains("Uploaded ITR differs from filed return", f.Title);
            Assert.Contains("business turnover", f.Explanation);

            // Evidence must be of Field kind with DocFieldID
            Assert.NotEmpty(f.Evidence);
            var ev = Assert.Single(f.Evidence.Where(e => e.Kind == EvidenceKind.Field));
            Assert.Equal(2, ev.RefId); // DocFieldID for business_turnover
            Assert.Equal(1, ev.CaseDocumentID);
            Assert.Equal(1, ev.PageNo);
            Assert.Contains("Business turnover", ev.Note);
        }

        [Fact]
        public void ItrMismatch_gross_total_income_differs_returns_high_finding()
        {
            var (doc, fields) = CreateUploadedItr(1, gti: 9500000);
            var itrJson = SampleItrJson(gti: 6237504);

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("ITR_MISMATCH", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains("gross total income", f.Explanation);
        }

        [Fact]
        public void ItrMismatch_total_income_differs_returns_high_finding()
        {
            var (doc, fields) = CreateUploadedItr(1, totalIncome: 8000000);
            var itrJson = SampleItrJson(totalIncome: 6212554);

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("ITR_MISMATCH", f.Type);
            Assert.Contains("total income", f.Explanation);
        }

        [Fact]
        public void ItrMismatch_tax_paid_differs_returns_high_finding()
        {
            var (doc, fields) = CreateUploadedItr(1, taxPaid: 3500000);
            var itrJson = SampleItrJson(taxPaid: 1959639);

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("ITR_MISMATCH", f.Type);
            Assert.Contains("taxes paid", f.Explanation);
        }

        [Fact]
        public void ItrMismatch_assessment_year_differs_returns_high_finding()
        {
            // Uploaded AY 2025-26 vs filed return AY 2026-27
            var (doc, fields) = CreateUploadedItr(1, ay: "2025-26");
            var itrJson = SampleItrJson(ay: "2026-27");

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);

            Assert.Single(findings);
            var f = findings[0];
            Assert.Equal("ITR_MISMATCH", f.Type);
            Assert.Contains("assessment year", f.Explanation);
        }

        [Fact]
        public void ItrMismatch_within_tolerance_returns_no_findings()
        {
            // 50 paise diff (within default ₹1 tolerance)
            var (doc, fields) = CreateUploadedItr(1, turnover: 46800000.50m);
            var itrJson = SampleItrJson(turnover: 46800000m);

            var findings = SourceChecks.ItrMismatch(new[] { doc }, fields, itrJson, 101);
            Assert.Empty(findings);
        }

        [Fact]
        public void ItrMismatch_missing_itr_json_or_no_docs_returns_empty()
        {
            var (doc, fields) = CreateUploadedItr(1);
            Assert.Empty(SourceChecks.ItrMismatch(new[] { doc }, fields, null, 101));
            Assert.Empty(SourceChecks.ItrMismatch(new[] { doc }, fields, "", 101));
            Assert.Empty(SourceChecks.ItrMismatch(Array.Empty<CaseDocument>(), fields, SampleItrJson(), 101));
            Assert.Empty(SourceChecks.ItrMismatch(new[] { doc }, Array.Empty<DocField>(), SampleItrJson(), 101));
        }

        [Fact]
        public void Render_Itr_generates_valid_pdf_bytes()
        {
            var pdf = FinRiskLensAI.DemoData.Render.Itr(
                "Sankalp Autotech Pvt Ltd", "AABCS1234F", "2026-27",
                6237504, 46800000, 6212554, 1959639,
                "Income Tax e-Filing Portal 3.0", "ITD e-Filing Utility",
                DateTime.UtcNow, DateTime.UtcNow);

            Assert.NotNull(pdf);
            Assert.NotEmpty(pdf);
            Assert.True(pdf.Length > 1000);
            // PDF starts with %PDF
            Assert.Equal((byte)'%', pdf[0]);
            Assert.Equal((byte)'P', pdf[1]);
            Assert.Equal((byte)'D', pdf[2]);
            Assert.Equal((byte)'F', pdf[3]);
        }
    }
}
