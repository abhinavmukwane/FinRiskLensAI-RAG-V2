using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Checks;

namespace FinRiskLensAI.Tests
{
    /// <summary>One hand-made statement per check — reference design §11: "every check leaves one unit test".</summary>
    public class DeterministicChecksTests
    {
        private static readonly FraudOptions O = new();
        private static readonly CaseDocument Doc = new() { CaseDocumentID = 7, LoanApplicationID = 1, FileName = "statement.pdf" };

        private static int _id;
        private static StatementRow Row(string date, decimal? credit, decimal? debit, decimal? balance,
                                        int page = 1, string? acct = null, string? name = null) => new()
        {
            StatementRowID = ++_id, CaseDocumentID = Doc.CaseDocumentID, PageNo = page, RowNo = _id,
            TxnDate = DateOnly.Parse(date), Description = "txn", Credit = credit, Debit = debit, Balance = balance,
            CounterpartyAcct = acct, CounterpartyName = name
        };

        /// <summary>An ordinary operating account from 1 Jan: salary in, spending out, balance always consistent.</summary>
        private static List<StatementRow> Clean(int days = 120)
        {
            var rows = new List<StatementRow>();
            decimal bal = 50_000;
            var d = new DateOnly(2026, 1, 1);
            for (int i = 0; i < days; i++, d = d.AddDays(1))
            {
                if (d.Day == 1) { bal += 80_000; rows.Add(Row(d.ToString("yyyy-MM-dd"), 80_000, null, bal, acct: "EMPLOYER-01")); }
                if (d.Day % 3 == 0) { bal -= 2_000; rows.Add(Row(d.ToString("yyyy-MM-dd"), null, 2_000, bal)); }
            }
            return rows;
        }

        [Fact]
        public void A_clean_statement_raises_nothing()
        {
            var app = new LoanApplication { LoanApplicationID = 1, SubmittedAt = new DateTime(2026, 4, 30), Documents = { Doc } };
            var findings = DeterministicChecks.Run(new CaseData(app, Clean(), Array.Empty<PageSummary>(), Array.Empty<DocField>()), O);
            Assert.Empty(findings);
        }

        [Fact]
        public void Edited_balance_breaks_continuity()
        {
            var rows = new List<StatementRow>
            {
                Row("2026-03-01", 10_000, null, 60_000),
                Row("2026-03-02", null, 5_000, 55_000),
                Row("2026-03-03", 20_000, null, 95_000),   // should be 75,000 — a zero was added
                Row("2026-03-04", null, 1_000, 94_000),
            };

            var f = DeterministicChecks.BalanceContinuity(Doc, rows, O)!;

            Assert.Equal("BALANCE_DISCONTINUITY", f.Type);
            Assert.Equal(FindingSeverity.High, f.Severity);
            Assert.Contains(f.Evidence, e => e.RefId == rows[2].StatementRowID);
            Assert.Contains("₹75,000.00", f.Explanation);
        }

        [Fact]
        public void Rows_without_a_printed_balance_carry_their_movement_forward()
        {
            var rows = new List<StatementRow>
            {
                Row("2026-03-01", 10_000, null, 60_000),
                Row("2026-03-01", null, 500, null),        // same-day rows, balance printed once
                Row("2026-03-01", null, 1_500, 58_000),
            };
            Assert.Null(DeterministicChecks.BalanceContinuity(Doc, rows, O));
        }

        [Fact]
        public void Rounding_within_tolerance_is_not_a_break()
        {
            var rows = new List<StatementRow> { Row("2026-03-01", null, null, 100.00m), Row("2026-03-02", 10.00m, null, 110.60m) };
            Assert.Null(DeterministicChecks.BalanceContinuity(Doc, rows, O));
        }

        [Fact]
        public void Page_that_does_not_carry_over()
        {
            var pages = new List<PageSummary>
            {
                new() { CaseDocumentID = 7, PageNo = 1, OpeningBalance = 10_000, ClosingBalance = 42_000 },
                new() { CaseDocumentID = 7, PageNo = 2, OpeningBalance = 92_000, ClosingBalance = 90_000 },
            };
            var f = DeterministicChecks.PageCarryOver(Doc, pages, O)!;
            Assert.Equal("PAGE_CARRY_MISMATCH", f.Type);
            Assert.Equal(new int?[] { 1, 2 }, f.Evidence.Select(e => e.PageNo));
        }

        [Fact]
        public void Footer_totals_that_do_not_match_the_rows()
        {
            var rows = new List<StatementRow> { Row("2026-03-01", 10_000, null, 10_000), Row("2026-03-02", null, 4_000, 6_000) };
            var pages = new List<PageSummary> { new() { CaseDocumentID = 7, PageNo = 1, StatedTotalCredit = 100_000, StatedTotalDebit = 4_000 } };

            var f = DeterministicChecks.StatedTotals(Doc, rows, pages, O)!;
            Assert.Equal("TOTALS_MISMATCH", f.Type);
            Assert.Single(f.Evidence);
            Assert.Contains("credits", f.Evidence[0].Note);
        }

        [Fact]
        public void Salary_slip_that_does_not_add_up()
        {
            var fields = new List<DocField>
            {
                new() { DocFieldID = 1, CaseDocumentID = 7, Name = "gross", Value = "85,000", NumValue = 85_000, PageNo = 1 },
                new() { DocFieldID = 2, CaseDocumentID = 7, Name = "deduction_pf", Value = "1,800", NumValue = 1_800, PageNo = 1 },
                new() { DocFieldID = 3, CaseDocumentID = 7, Name = "deduction_tds", Value = "6,200", NumValue = 6_200, PageNo = 1 },
                new() { DocFieldID = 4, CaseDocumentID = 7, Name = "net", Value = "97,000", NumValue = 97_000, PageNo = 1 },
            };
            var f = DeterministicChecks.SlipArithmetic(Doc, fields, O)!;
            Assert.Equal("SLIP_ARITHMETIC_ERROR", f.Type);
            Assert.Contains("₹77,000.00", f.Explanation);

            fields[3].NumValue = 77_000;
            Assert.Null(DeterministicChecks.SlipArithmetic(Doc, fields, O));
        }

        [Fact]
        public void Credit_sent_back_to_its_payer()
        {
            var rows = new List<StatementRow>
            {
                Row("2026-03-01", 150_000, null, 160_000, acct: "XX4471", name: "Shree Traders"),
                Row("2026-03-02", null, 2_000, 158_000),
                Row("2026-03-03", null, 145_000, 13_000, acct: "XX4471", name: "SHREE TRADERS"),
                Row("2026-03-20", null, 5_000, 8_000, acct: "XX4471"),   // later, small: not a return
            };
            var f = DeterministicChecks.RoundTripping(Doc, rows, O)!;
            Assert.Equal("SALARY_ROUNDTRIP", f.Type);
            Assert.Equal(new[] { rows[0].StatementRowID, rows[2].StatementRowID }, f.Evidence.Select(e => e.RefId!.Value));
        }

        [Fact]
        public void Deposit_parked_just_before_the_application_then_withdrawn()
        {
            var rows = Clean(days: 98);   // 1 Jan – 8 Apr
            var last = rows[^1];
            decimal bal = last.Balance!.Value;
            // 9 Apr: a 20-lakh deposit lands; 12 Apr: it leaves again. Application on 20 Apr.
            rows.Add(Row("2026-04-09", 2_000_000, null, bal += 2_000_000));
            rows.Add(Row("2026-04-12", null, 1_900_000, bal -= 1_900_000));
            rows.Add(Row("2026-04-19", 2_500_000, null, bal += 2_500_000));
            var app = new LoanApplication { LoanApplicationID = 1, SubmittedAt = new DateTime(2026, 4, 20) };

            var f = DeterministicChecks.WindowDressing(Doc, rows, app.SubmittedAt, O)!;
            Assert.Equal("PRE_APPLICATION_WINDOW_DRESSING", f.Type);
            Assert.Single(f.Evidence);   // the 19 Apr credit never left, so only 9 Apr is evidence
        }

        [Fact]
        public void Mule_account_forwards_what_it_receives()
        {
            var rows = new List<StatementRow>();
            decimal bal = 1_000;
            for (int i = 1; i <= 12; i++)
            {
                var day = $"2026-03-{i:00}";
                rows.Add(Row(day, 50_000, null, bal += 50_000));
                rows.Add(Row(day, null, 49_000, bal -= 49_000));
            }
            var f = DeterministicChecks.PassThrough(Doc, rows, O)!;
            Assert.Equal("PASS_THROUGH", f.Type);

            Assert.Null(DeterministicChecks.PassThrough(Doc, Clean(), O));
        }

        [Fact]
        public void File_produced_by_a_consumer_editor_and_modified_later()
        {
            var edited = new CaseDocument
            {
                CaseDocumentID = 9, LoanApplicationID = 1, FileName = "slip.pdf",
                PdfProducer = "Microsoft Word for Microsoft 365", PdfCreator = "Microsoft Word",
                PdfCreatedAt = new DateTime(2026, 9, 1, 4, 30, 0), PdfModifiedAt = new DateTime(2026, 9, 20, 10, 0, 0)
            };
            var f = DeterministicChecks.Metadata(edited, O)!;
            Assert.Equal(FindingSeverity.Medium, f.Severity);
            Assert.Contains("Microsoft Word", f.Explanation);
            Assert.Contains("19 days", f.Explanation);

            var bank = new CaseDocument { CaseDocumentID = 10, PdfProducer = "iText 7 (HDFC NetBanking)",
                                          PdfCreatedAt = edited.PdfCreatedAt, PdfModifiedAt = edited.PdfCreatedAt };
            Assert.Null(DeterministicChecks.Metadata(bank, O));
        }

        [Fact]
        public void Findings_are_keyed_per_document_so_reruns_replace_them()
        {
            var f = DeterministicChecks.Metadata(new CaseDocument { CaseDocumentID = 9, PdfProducer = "Canva" }, O)!;
            Assert.Equal("METADATA_ANOMALY:doc9", f.DedupKey);
        }
    }
}
