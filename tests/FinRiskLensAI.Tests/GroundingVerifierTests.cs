using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Ai;

namespace FinRiskLensAI.Tests
{
    /// <summary>R7 / step ⑥: an AI claim reaches the investigator only if this case's own data backs it.</summary>
    public class GroundingVerifierTests
    {
        private static StatementRow Row(long id, decimal? debit, string date = "2026-07-05") => new()
        {
            StatementRowID = id, CaseDocumentID = 3, PageNo = 1, RowNo = (int)id, TxnDate = DateOnly.Parse(date),
            Description = "NACH DR SWIFT DEMO FINANCE", Debit = debit
        };

        private static GroundingContext Ctx() => new()
        {
            LoanApplicationID = 9,
            Rows = new[] { Row(101, 18_500), Row(102, 18_500, "2026-08-05"), Row(103, 18_500, "2026-09-05") }.ToDictionary(r => r.StatementRowID),
            KnownAmounts = new HashSet<decimal> { 0m },
            RetrievedChunks = new Dictionary<long, string> { [31] = "Sample Credit Policy v3 › §3.1 FOIR" }
        };

        private static ProposedFinding Emi(params long[] rows) => new()
        {
            Type = "undeclared_emi", Severity = "High",
            Title = "₹18,500 monthly EMI to Swift Demo Finance not declared",
            Explanation = "Three NACH debits of ₹18,500 (₹55,500 in total) to a lender; the application declares no EMIs.",
            RecommendedAction = "Add the EMI to FOIR as policy requires.",
            Evidence = rows.Select(id => new ProposedEvidence { Kind = "row", Id = id }).ToList(),
            Citations = new() { 31 }
        };

        [Fact]
        public void A_grounded_finding_is_kept_with_its_evidence_and_policy_citation()
        {
            var r = GroundingVerifier.Verify(new[] { Emi(101, 102, 103) }, Ctx());

            var f = Assert.Single(r.Kept);
            Assert.Equal("UNDECLARED_EMI", f.Type);
            Assert.Equal(FindingCategory.HiddenLiabilities, f.Category);
            Assert.Equal(FindingSource.AiVerified, f.DetectedBy);
            Assert.Equal(3, f.Evidence.Count(e => e.Kind == EvidenceKind.Row));
            Assert.Contains(f.Evidence, e => e.Kind == EvidenceKind.Policy && e.RefId == 31);
            Assert.Empty(r.Dropped);
        }

        [Fact]
        public void A_row_from_outside_the_case_drops_the_finding()
        {
            var r = GroundingVerifier.Verify(new[] { Emi(101, 999) }, Ctx());
            Assert.Empty(r.Kept);
            Assert.Contains("999", Assert.Single(r.Dropped).Reason);
        }

        [Fact]
        public void An_invented_amount_drops_the_finding()
        {
            var p = Emi(101, 102, 103);
            p.Explanation = "Debits of ₹18,500 a month, ₹2,40,000 over the year."; // never in the data
            var r = GroundingVerifier.Verify(new[] { p }, Ctx());
            Assert.Empty(r.Kept);
            Assert.Contains("240,000", Assert.Single(r.Dropped).Reason);
        }

        [Fact]
        public void The_total_of_the_cited_rows_is_a_valid_amount()
        {
            var r = GroundingVerifier.Verify(new[] { Emi(101, 102, 103) }, Ctx()); // quotes ₹55,500 = 3 × 18,500
            Assert.Single(r.Kept);
        }

        [Fact]
        public void One_counterpartys_total_is_valid_when_rows_of_two_are_cited()
        {
            var ctx = Ctx();
            var rows = ctx.Rows.Values.Select(r => { r.CounterpartyName = "Swift Demo Finance"; return r; })
                          .Append(new StatementRow { StatementRowID = 104, CaseDocumentID = 3, PageNo = 2, RowNo = 104,
                                                     TxnDate = new DateOnly(2026, 9, 12), Description = "NACH DR KREDITPAL",
                                                     Debit = 12_200, CounterpartyName = "KreditPal Demo NBFC" })
                          .ToDictionary(r => r.StatementRowID);
            var p = Emi(101, 102, 103, 104);
            p.Explanation = "₹55,500 to Swift Demo Finance and ₹12,200 to KreditPal; none declared.";

            var r = GroundingVerifier.Verify(new[] { p }, new GroundingContext
            {
                LoanApplicationID = 9, Rows = rows, KnownAmounts = ctx.KnownAmounts, RetrievedChunks = ctx.RetrievedChunks
            });
            Assert.Single(r.Kept);
        }

        [Fact]
        public void Types_owned_by_the_deterministic_checks_are_refused()
        {
            var p = Emi(101, 102, 103);
            p.Type = "BALANCE_DISCONTINUITY";
            Assert.Empty(GroundingVerifier.Verify(new[] { p }, Ctx()).Kept);
        }

        [Fact]
        public void A_citation_the_search_never_returned_is_stripped_not_fatal()
        {
            var p = Emi(101, 102, 103);
            p.Citations = new() { 31, 777 };
            var r = GroundingVerifier.Verify(new[] { p }, Ctx());

            Assert.Equal(1, r.CitationsStripped);
            Assert.DoesNotContain(Assert.Single(r.Kept).Evidence, e => e.RefId == 777);
        }

        [Fact]
        public void Two_findings_of_one_type_on_one_document_keep_only_the_first()
        {
            var r = GroundingVerifier.Verify(new[] { Emi(101, 102, 103), Emi(103, 102, 101) }, Ctx());
            Assert.Single(r.Kept);
            Assert.Contains("duplicates", Assert.Single(r.Dropped).Reason);
        }

        [Fact]
        public void Rupee_amounts_are_read_in_indian_grouping()
            => Assert.Equal(new[] { 123456.78m, 45000m, 2500m }, GroundingVerifier.Amounts("₹1,23,456.78 then Rs. 45,000 and INR 2500"));
    }
}
