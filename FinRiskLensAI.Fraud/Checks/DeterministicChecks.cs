using System.Globalization;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Fraud.Checks
{
    /// <summary>
    /// Tolerances and thresholds for the deterministic checks — config, because bank
    /// formats differ (rounding, date conventions). Bound from the "Fraud" section.
    /// </summary>
    public class FraudOptions
    {
        public decimal AmountTolerance { get; set; } = 1.00m;
        public int RoundTripDays { get; set; } = 3;
        public decimal RoundTripMinShare { get; set; } = 0.8m;
        public int WindowDays { get; set; } = 15;
        public int BaselineDays { get; set; } = 75;
        public decimal WindowMultiplier { get; set; } = 2m;
        public int WindowExitDays { get; set; } = 10;
        public decimal PassThroughShare { get; set; } = 0.8m;
        public int PassThroughMinCredits { get; set; } = 10;
        public int ModifiedGraceMinutes { get; set; } = 1;
        /// <summary>Days a bank's posting date may lag the date printed on the statement (clearing, value dates).</summary>
        public int AaDateSlackDays { get; set; } = 1;
        /// <summary>When no account number ties a statement to an AA account, the share of its rows that must agree before it counts as the same account.</summary>
        public decimal AaMinMatchShare { get; set; } = 0.5m;
        /// <summary>Maximum allowed ratio of monthly bank credits to the higher of GST or ITR turnover before flagging income inflation.</summary>
        public decimal BankToTurnoverMax { get; set; } = 1.5m;
        /// <summary>Maximum allowed percentage discrepancy between GST turnover and ITR business turnover.</summary>
        public decimal GstItrVariancePct { get; set; } = 25.0m;
        /// <summary>Producer/creator substrings that mean a consumer tool touched the file, not a bank system.</summary>
        public string[] ConsumerEditors { get; set; } =
        {
            "Microsoft Word", "Microsoft: Print To PDF", "Photoshop", "Illustrator", "Canva", "iLovePDF",
            "Smallpdf", "Sejda", "PDFescape", "PDF-XChange Editor", "Foxit PhantomPDF", "Nitro", "LibreOffice",
            "WPS", "Google Docs", "Acrobat Pro"
        };
    }

    /// <summary>
    /// Everything the checks read for one application. <paramref name="BankRecord"/> is the bank's own
    /// transactions for the linked Udyam number, fetched through Account Aggregator, when there are any.
    /// </summary>
    public sealed record CaseData(
        LoanApplication Application,
        IReadOnlyList<StatementRow> Rows,
        IReadOnlyList<PageSummary> Pages,
        IReadOnlyList<DocField> Fields,
        IReadOnlyList<AaTransaction>? BankRecord = null);

    /// <summary>
    /// The deterministic checks (Doc/10_FRAUD_LENS_BUILD.md §4, reference design §11).
    /// Pure functions over one case's rows, pages and fields: a case is at most a few
    /// thousand rows, so C# over memory is as fast as the SQL would be and every check
    /// is unit-testable without a database. One finding per document per check, with
    /// every offending row as evidence — a report of forty identical cards helps nobody.
    /// </summary>
    public static class DeterministicChecks
    {
        private const int MaxEvidence = 25;
        private static readonly CultureInfo InIndia = new("en-IN");

        public static List<Finding> Run(CaseData data, FraudOptions o)
        {
            var findings = new List<Finding>();
            var docs = data.Application.Documents;

            foreach (var doc in docs)
            {
                var rows = data.Rows.Where(r => r.CaseDocumentID == doc.CaseDocumentID).OrderBy(r => r.RowNo).ToList();
                var pages = data.Pages.Where(p => p.CaseDocumentID == doc.CaseDocumentID).OrderBy(p => p.PageNo).ToList();
                var fields = data.Fields.Where(f => f.CaseDocumentID == doc.CaseDocumentID).ToList();

                Add(findings, BalanceContinuity(doc, rows, o));
                Add(findings, PageCarryOver(doc, pages, o));
                Add(findings, StatedTotals(doc, rows, pages, o));
                Add(findings, SlipArithmetic(doc, fields, o));
                Add(findings, RoundTripping(doc, rows, o));
                Add(findings, WindowDressing(doc, rows, data.Application.SubmittedAt, o));
                Add(findings, PassThrough(doc, rows, o));
                Add(findings, Metadata(doc, o));
                if (data.BankRecord != null) Add(findings, BankRecordMismatch(doc, rows, fields, data.BankRecord, o));
            }
            return findings;
        }

        private static void Add(List<Finding> list, Finding? f) { if (f != null) list.Add(f); }

        // ── 1. Running balance ≠ previous balance ± amount ──────────────────────────
        public static Finding? BalanceContinuity(CaseDocument doc, IReadOnlyList<StatementRow> rows, FraudOptions o)
        {
            var breaks = new List<(StatementRow Prev, StatementRow Row, decimal Expected)>();
            StatementRow? anchor = null;    // last row that printed a balance
            decimal pending = 0;            // net movement since the anchor, for rows with no balance printed

            foreach (var row in rows)
            {
                var movement = (row.Credit ?? 0) - (row.Debit ?? 0);
                if (row.Balance == null) { pending += movement; continue; }

                if (anchor != null)
                {
                    var expected = anchor.Balance!.Value + pending + movement;
                    if (Math.Abs(expected - row.Balance.Value) > o.AmountTolerance)
                        breaks.Add((anchor, row, expected));
                }
                anchor = row;
                pending = 0;
            }
            if (breaks.Count == 0) return null;

            var first = breaks[0];
            return New(doc, "BALANCE_DISCONTINUITY", FindingCategory.Tampering, FindingSeverity.High,
                $"Running balance breaks {Times(breaks.Count)} on {doc.FileName}",
                $"The printed balance does not follow from the previous balance and the transaction amount. " +
                $"First break: page {first.Row.PageNo}, row {first.Row.RowNo} prints {Rs(first.Row.Balance)} where " +
                $"{Rs(first.Prev.Balance)} {Signed((first.Row.Credit ?? 0) - (first.Row.Debit ?? 0))} gives {Rs(first.Expected)}. " +
                "A genuine bank statement is generated from the ledger and never breaks its own arithmetic.",
                "Obtain the statement directly from the bank or through Account Aggregator and compare the rows flagged.",
                breaks.SelectMany(b => new[] { RowEvidence(b.Prev, "previous balance"), RowEvidence(b.Row, $"expected {Rs(b.Expected)}") }));
        }

        // ── 2. Page closing ≠ next page opening ─────────────────────────────────────
        public static Finding? PageCarryOver(CaseDocument doc, IReadOnlyList<PageSummary> pages, FraudOptions o)
        {
            var breaks = pages.Zip(pages.Skip(1))
                .Where(p => p.First.ClosingBalance != null && p.Second.OpeningBalance != null
                         && p.Second.PageNo == p.First.PageNo + 1
                         && Math.Abs(p.First.ClosingBalance.Value - p.Second.OpeningBalance.Value) > o.AmountTolerance)
                .ToList();
            if (breaks.Count == 0) return null;

            var (a, b) = breaks[0];
            return New(doc, "PAGE_CARRY_MISMATCH", FindingCategory.Tampering, FindingSeverity.High,
                $"Balance carried between pages does not match on {doc.FileName}",
                $"Page {a.PageNo} closes at {Rs(a.ClosingBalance)} but page {b.PageNo} opens at {Rs(b.OpeningBalance)}" +
                (breaks.Count > 1 ? $", and {breaks.Count - 1} more page boundar{(breaks.Count == 2 ? "y" : "ies")} disagree." : ".") +
                " Pages assembled from different statements, or a page edited in isolation, break the carry-over.",
                "Check whether the pages come from one continuous statement period.",
                breaks.SelectMany(p => new[] { PageEvidence(doc, p.First.PageNo, $"closes {Rs(p.First.ClosingBalance)}"),
                                               PageEvidence(doc, p.Second.PageNo, $"opens {Rs(p.Second.OpeningBalance)}") }));
        }

        // ── 3. Stated page totals ≠ sum of the rows ─────────────────────────────────
        public static Finding? StatedTotals(CaseDocument doc, IReadOnlyList<StatementRow> rows, IReadOnlyList<PageSummary> pages, FraudOptions o)
        {
            var misses = new List<(int Page, string Side, decimal Stated, decimal Sum)>();
            foreach (var p in pages)
            {
                var onPage = rows.Where(r => r.PageNo == p.PageNo).ToList();
                if (onPage.Count == 0) continue;
                var debit = onPage.Sum(r => r.Debit ?? 0);
                var credit = onPage.Sum(r => r.Credit ?? 0);
                if (p.StatedTotalDebit is { } sd && Math.Abs(sd - debit) > o.AmountTolerance) misses.Add((p.PageNo, "debits", sd, debit));
                if (p.StatedTotalCredit is { } sc && Math.Abs(sc - credit) > o.AmountTolerance) misses.Add((p.PageNo, "credits", sc, credit));
            }
            if (misses.Count == 0) return null;

            var m = misses[0];
            return New(doc, "TOTALS_MISMATCH", FindingCategory.Tampering, FindingSeverity.High,
                $"Stated totals do not add up on {doc.FileName}",
                $"Page {m.Page} states total {m.Side} of {Rs(m.Stated)}, but its rows sum to {Rs(m.Sum)}" +
                (misses.Count > 1 ? $"; {misses.Count - 1} more total{(misses.Count == 2 ? "" : "s")} disagree." : ".") +
                " Editing a transaction without also editing the footer leaves exactly this trace.",
                "Compare the flagged page against the bank's own copy.",
                misses.Select(x => PageEvidence(doc, x.Page, $"stated {x.Side} {Rs(x.Stated)}, rows sum {Rs(x.Sum)}")));
        }

        // ── 4. Salary slip: gross − deductions ≠ net ────────────────────────────────
        public static Finding? SlipArithmetic(CaseDocument doc, IReadOnlyList<DocField> fields, FraudOptions o)
        {
            var gross = Field(fields, "gross");
            var net = Field(fields, "net");
            if (gross?.NumValue == null || net?.NumValue == null) return null;

            // Extractor contract: either one total_deductions field or itemised deduction_* fields
            var total = Field(fields, "total_deductions");
            var items = fields.Where(f => f.Name.StartsWith("deduction_", StringComparison.OrdinalIgnoreCase) && f.NumValue != null).ToList();
            var deductions = total?.NumValue ?? items.Sum(f => f.NumValue!.Value);
            var expected = gross.NumValue.Value - deductions;
            if (Math.Abs(expected - net.NumValue.Value) <= o.AmountTolerance) return null;

            var evidence = new List<Evidence> { FieldEvidence(gross, "gross"), FieldEvidence(net, $"expected {Rs(expected)}") };
            if (total != null) evidence.Add(FieldEvidence(total, "total deductions"));
            else evidence.AddRange(items.Select(f => FieldEvidence(f, f.Name)));

            return New(doc, "SLIP_ARITHMETIC_ERROR", FindingCategory.Fabrication, FindingSeverity.High,
                $"Salary slip does not add up: {doc.FileName}",
                $"Gross pay {Rs(gross.NumValue)} less deductions {Rs(deductions)} is {Rs(expected)}, but the slip prints net pay " +
                $"{Rs(net.NumValue)}. Payroll systems compute net pay; a hand-edited slip often changes one figure and not the others.",
                "Verify the salary with the employer, or against the salary credit on the bank statement.",
                evidence);
        }

        // ── 5. Credit sent back to its payer within days ────────────────────────────
        public static Finding? RoundTripping(CaseDocument doc, IReadOnlyList<StatementRow> rows, FraudOptions o)
        {
            var pairs = new List<(StatementRow Credit, StatementRow Debit)>();
            var used = new HashSet<long>();
            foreach (var c in rows.Where(r => r.Credit > 0 && Counterparty(r) != null))
            {
                var d = rows.FirstOrDefault(r => r.Debit >= o.RoundTripMinShare * c.Credit
                                              && !used.Contains(r.StatementRowID)
                                              && r.TxnDate >= c.TxnDate && r.TxnDate <= c.TxnDate.AddDays(o.RoundTripDays)
                                              && r.RowNo > c.RowNo
                                              && Counterparty(r) == Counterparty(c));
                if (d == null) continue;
                used.Add(d.StatementRowID);
                pairs.Add((c, d));
            }
            if (pairs.Count == 0) return null;

            var total = pairs.Sum(p => p.Credit.Credit!.Value);
            var (pc, pd) = pairs[0];
            return New(doc, "SALARY_ROUNDTRIP", FindingCategory.IncomeInflation, FindingSeverity.High,
                $"Money returned to its payer {Times(pairs.Count)} on {doc.FileName}",
                $"{Rs(total)} credited and sent back to the same counterparty within {o.RoundTripDays} days. First: " +
                $"{Rs(pc.Credit)} in on {pc.TxnDate:dd MMM yyyy}, {Rs(pd.Debit)} out on {pd.TxnDate:dd MMM yyyy} " +
                $"({pc.CounterpartyName ?? pc.CounterpartyAcct}). Income that goes straight back to its source is not income.",
                "Exclude the round-tripped credits from assessed income and ask the applicant to explain the payer relationship.",
                pairs.SelectMany(p => new[] { RowEvidence(p.Credit, "credit in"), RowEvidence(p.Debit, "returned") }));
        }

        // ── 6. Balance pumped up just before the application, then drained ──────────
        public static Finding? WindowDressing(CaseDocument doc, IReadOnlyList<StatementRow> rows, DateTime submittedAt, FraudOptions o)
        {
            var eod = EndOfDayBalances(rows);
            if (eod.Count == 0) return null;

            var anchor = DateOnly.FromDateTime(submittedAt) < eod.Keys.Max() ? DateOnly.FromDateTime(submittedAt) : eod.Keys.Max();
            var windowStart = anchor.AddDays(-o.WindowDays + 1);
            var baselineStart = windowStart.AddDays(-o.BaselineDays);
            if (eod.Keys.Min() > baselineStart) return null;   // not enough history to call a spike

            var window = AverageBalance(eod, windowStart, anchor);
            var baseline = AverageBalance(eod, baselineStart, windowStart.AddDays(-1));
            if (baseline <= 0 || window <= o.WindowMultiplier * baseline) return null;

            // The credits that did it — large against the normal balance — and whether they left again
            var exits = rows.Where(r => r.Credit >= baseline && r.TxnDate >= windowStart && r.TxnDate <= anchor)
                .Select(c => (Credit: c, Out: rows.Where(d => d.Debit > 0 && d.RowNo > c.RowNo
                                                          && d.TxnDate <= c.TxnDate.AddDays(o.WindowExitDays))
                                                  .Sum(d => d.Debit!.Value)))
                .Where(x => x.Out >= o.RoundTripMinShare * x.Credit.Credit!.Value)
                .ToList();
            if (exits.Count == 0) return null;

            return New(doc, "PRE_APPLICATION_WINDOW_DRESSING", FindingCategory.IncomeInflation, FindingSeverity.Medium,
                $"Balance inflated just before the application on {doc.FileName}",
                $"The average balance over the last {o.WindowDays} days before {anchor:dd MMM yyyy} was {Rs(window)}, " +
                $"{window / baseline:0.0}× the {Rs(baseline)} average of the {o.BaselineDays} days before. " +
                $"{Times(exits.Count, "large credit")} arrived in that window and left within {o.WindowExitDays} days. " +
                "Average-balance tests read this as a healthy account; it is a temporary deposit.",
                "Assess the average balance excluding the window, and ask where the large credits came from.",
                exits.Select(x => RowEvidence(x.Credit, $"left within {o.WindowExitDays} days")));
        }

        // ── 7. Most money leaves within a day of arriving ───────────────────────────
        public static Finding? PassThrough(CaseDocument doc, IReadOnlyList<StatementRow> rows, FraudOptions o)
        {
            var credits = rows.Where(r => r.Credit > 0).ToList();
            if (credits.Count < o.PassThroughMinCredits) return null;

            // Greedy: each debit can explain only one credit's exit
            var remaining = rows.Where(r => r.Debit > 0).ToDictionary(r => r.StatementRowID, r => r.Debit!.Value);
            decimal passed = 0;
            foreach (var c in credits)
            {
                var need = c.Credit!.Value;
                foreach (var d in rows.Where(r => r.Debit > 0 && r.RowNo > c.RowNo && r.TxnDate <= c.TxnDate.AddDays(1)))
                {
                    if (need <= 0) break;
                    var take = Math.Min(need, remaining[d.StatementRowID]);
                    remaining[d.StatementRowID] -= take;
                    need -= take;
                }
                passed += c.Credit.Value - need;
            }

            var totalIn = credits.Sum(c => c.Credit!.Value);
            var share = passed / totalIn;
            if (share <= o.PassThroughShare) return null;

            return New(doc, "PASS_THROUGH", FindingCategory.Behavioural, FindingSeverity.Medium,
                $"Account behaves like a pass-through on {doc.FileName}",
                $"{share:P0} of the {Rs(totalIn)} credited left the account within a day of arriving, across {credits.Count} credits. " +
                "An operating account keeps a working balance; a mule account forwards what it receives.",
                "Review the counterparties on both sides of the flow before relying on the turnover.",
                new[] { new Evidence { Kind = EvidenceKind.Doc, RefId = doc.CaseDocumentID, CaseDocumentID = doc.CaseDocumentID,
                                       Note = $"{share:P0} passed through" } });
        }

        // ── 8. The file itself: produced or edited by a consumer tool ───────────────
        public static Finding? Metadata(CaseDocument doc, FraudOptions o)
        {
            var reasons = new List<string>();
            var editor = o.ConsumerEditors.FirstOrDefault(e =>
                (doc.PdfProducer?.Contains(e, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (doc.PdfCreator?.Contains(e, StringComparison.OrdinalIgnoreCase) ?? false));
            if (editor != null)
                reasons.Add($"it was produced by {editor} (producer “{doc.PdfProducer}”, creator “{doc.PdfCreator}”), not a bank or payroll system");

            var modified = doc.PdfCreatedAt != null && doc.PdfModifiedAt != null
                        && doc.PdfModifiedAt > doc.PdfCreatedAt.Value.AddMinutes(o.ModifiedGraceMinutes);
            if (modified)
                reasons.Add($"it was modified on {doc.PdfModifiedAt:dd MMM yyyy HH:mm}, {Gap(doc.PdfModifiedAt!.Value - doc.PdfCreatedAt!.Value)} after it was created");

            if (reasons.Count == 0) return null;

            return New(doc, "METADATA_ANOMALY", FindingCategory.Tampering,
                editor != null ? FindingSeverity.Medium : FindingSeverity.Low,
                $"File history suggests editing: {doc.FileName}",
                "The PDF's own metadata says " + string.Join("; and ", reasons) + ". " +
                "Metadata alone does not prove tampering — read it alongside the content findings.",
                "Ask for the original download, or fetch the same document from its source.",
                new[] { new Evidence { Kind = EvidenceKind.Doc, RefId = doc.CaseDocumentID, CaseDocumentID = doc.CaseDocumentID,
                                       PageNo = 1, Note = doc.PdfProducer } });
        }

        // ── 9. The statement against the bank's own record (Account Aggregator) ─────
        /// <summary>
        /// The applicant can edit a PDF; they cannot edit what the bank sends through Account
        /// Aggregator. A careful forgery recomputes every balance and total, so checks 1–3 pass —
        /// but to be worth making it has to disagree with the bank somewhere. Rows are matched on
        /// side, amount and date (within <see cref="FraudOptions.AaDateSlackDays"/>); narrations differ
        /// between a bank's PDF and its AA feed, so they are not compared.
        /// </summary>
        public static Finding? BankRecordMismatch(CaseDocument doc, IReadOnlyList<StatementRow> rows, IReadOnlyList<DocField> fields,
                                                  IReadOnlyList<AaTransaction> bankRecord, FraudOptions o)
        {
            // Which AA account is this statement? The one with the same last four digits; failing that
            // (none printed, or a feed without account numbers) the one most of its rows agree with.
            var printed = LastFour(Field(fields, "account_number")?.Value);
            var best = bankRecord.Where(t => !t.IsUnpassed).GroupBy(t => t.AccountRef)
                .Select(a => (Account: a.Key, SameNumber: printed != null && printed == LastFour(a.Key), Match: MatchBank(rows, a.ToList(), o)))
                .Where(x => x.Match != null && (x.SameNumber || x.Match.Agree >= o.AaMinMatchShare * x.Match.Rows))
                .OrderByDescending(x => x.SameNumber).ThenByDescending(x => x.Match!.Agree)
                .FirstOrDefault();
            var m = best.Match;
            if (m == null) return null;   // not an account the applicant shared through AA, or no common dates
            if (m.Altered.Count + m.OnlyOnPdf.Count + m.OnlyAtBank.Count == 0) return null;

            var parts = new List<string>();
            if (m.Altered.Count > 0) parts.Add($"{Times(m.Altered.Count, "amount")} changed");
            if (m.OnlyOnPdf.Count > 0) parts.Add($"{Times(m.OnlyOnPdf.Count, "transaction")} the bank never recorded");
            if (m.OnlyAtBank.Count > 0) parts.Add($"{Times(m.OnlyAtBank.Count, "transaction")} the bank recorded left out");

            string first;
            if (m.Altered.Count > 0)
            {
                var (r, b) = m.Altered[0];
                first = $"row {r.RowNo} on {r.TxnDate:dd MMM yyyy} shows a {Side(r)} of {Rs(Amount(r))} where the bank recorded {Rs(b.Amount)}";
            }
            else if (m.OnlyOnPdf.Count > 0)
            {
                var r = m.OnlyOnPdf[0];
                first = $"the {Side(r)} of {Rs(Amount(r))} on {r.TxnDate:dd MMM yyyy} (row {r.RowNo}) is not in the bank's record";
            }
            else
            {
                var b = m.OnlyAtBank[0];
                first = $"the bank recorded a {b.Direction.ToLowerInvariant()} of {Rs(b.Amount)} on {b.Date:dd MMM yyyy} (“{b.Narration}”) that the PDF leaves out";
            }

            var effect = new List<string>();
            if (Math.Abs(m.CreditGap) > o.AmountTolerance) effect.Add($"{Rs(Math.Abs(m.CreditGap))} {(m.CreditGap > 0 ? "more" : "less")} in credits");
            if (Math.Abs(m.DebitGap) > o.AmountTolerance) effect.Add($"{Rs(Math.Abs(m.DebitGap))} {(m.DebitGap > 0 ? "more" : "less")} in debits");

            return New(doc, "STATEMENT_VS_AA_MISMATCH", FindingCategory.Tampering, FindingSeverity.High,
                $"Statement disagrees with the bank's own record: {doc.FileName}",
                $"Compared with {(best.Account is { Length: > 0 } a ? "account " + a : "the account")} as the bank itself reported it through Account Aggregator, " +
                $"{m.From:dd MMM yyyy} to {m.To:dd MMM yyyy}: {m.Agree} of {m.Rows} rows agree, with {string.Join(", ", parts.SkipLast(1))}{(parts.Count > 1 ? " and " : "")}{parts[^1]}. First: {first}." +
                (effect.Count > 0 ? $" Net effect: the PDF shows {string.Join(" and ", effect)} than the bank recorded." : "") +
                " Balances and totals can be recomputed to hide an edit; the bank's own record cannot be.",
                "Assess the application on the Account Aggregator data, not the uploaded PDF, and ask the applicant to explain every difference listed.",
                m.Altered.Select(x => RowEvidence(x.Row, $"bank recorded {Rs(x.Bank.Amount)}"))
                 .Concat(m.OnlyAtBank.Select(b => PageEvidence(doc,
                     rows.LastOrDefault(r => r.TxnDate <= DateOnly.FromDateTime(b.Date))?.PageNo ?? rows[0].PageNo,
                     $"bank recorded {b.Date:dd MMM yyyy}: {b.Direction.ToLowerInvariant()} {Rs(b.Amount)} “{b.Narration}” — not on the statement")))
                 .Concat(m.OnlyOnPdf.Select(r => RowEvidence(r, "not in the bank's record"))));
        }

        private sealed record BankMatch(DateOnly From, DateOnly To, int Rows, int Agree,
                                        List<(StatementRow Row, AaTransaction Bank)> Altered,
                                        List<StatementRow> OnlyOnPdf, List<AaTransaction> OnlyAtBank,
                                        decimal CreditGap, decimal DebitGap);

        /// <summary>One statement against one AA account, over the dates both cover; null when they share none.</summary>
        private static BankMatch? MatchBank(IReadOnlyList<StatementRow> rows, List<AaTransaction> account, FraudOptions o)
        {
            static DateOnly Day(AaTransaction t) => DateOnly.FromDateTime(t.Date);

            var moving = rows.Where(r => r.Credit > 0 || r.Debit > 0).ToList();
            if (moving.Count == 0 || account.Count == 0) return null;

            // A statement may run past the consent period or start before it: judge only the days both
            // cover, but match across the slack either side, so a posting that lags past the edge still pairs
            var from = new[] { moving.Min(r => r.TxnDate), account.Min(Day) }.Max();
            var to = new[] { moving.Max(r => r.TxnDate), account.Max(Day) }.Min();
            if (from > to) return null;
            bool Inside(DateOnly d) => d >= from && d <= to;
            var (lo, hi) = (from.AddDays(-o.AaDateSlackDays), to.AddDays(o.AaDateSlackDays));

            var pdf = moving.Where(r => r.TxnDate >= lo && r.TxnDate <= hi).ToList();
            var bank = account;
            var judged = pdf.Count(r => Inside(r.TxnDate));
            if (judged == 0) return null;
            var used = new bool[bank.Count];

            // Nearest unused bank transaction on the same side that fits, within the date slack unless anyDate
            int Find(StatementRow r, Func<AaTransaction, bool> fits, bool anyDate = false)
            {
                int found = -1, gap = int.MaxValue;
                for (int i = 0; i < bank.Count; i++)
                {
                    if (used[i] || bank[i].IsCredit != r.Credit > 0 || !fits(bank[i])) continue;
                    var g = Math.Abs(Day(bank[i]).DayNumber - r.TxnDate.DayNumber);
                    if ((anyDate || g <= o.AaDateSlackDays) && g < gap) { found = i; gap = g; }
                }
                return found;
            }
            bool Near(decimal? a, decimal? b) => a != null && b != null && Math.Abs(a.Value - b.Value) <= o.AmountTolerance;

            var unmatched = new List<StatementRow>();
            foreach (var r in pdf)
            {
                var i = Find(r, t => Near(t.Amount, Amount(r)));
                // A misread date is not a disagreement: the same amount leaving the same running balance
                // is the same posting. A forger who changes an amount has to recompute the balances after it.
                if (i < 0) i = Find(r, t => Near(t.Amount, Amount(r)) && Near(t.Balance, r.Balance), anyDate: true);
                if (i >= 0) used[i] = true;
                else if (Inside(r.TxnDate)) unmatched.Add(r);
            }

            // Left over on both sides, same day and side: one transaction with its amount changed
            var altered = new List<(StatementRow Row, AaTransaction Bank)>();
            var onlyOnPdf = new List<StatementRow>();
            foreach (var r in unmatched)
            {
                var i = Find(r, _ => true);
                if (i < 0) onlyOnPdf.Add(r); else { used[i] = true; altered.Add((r, bank[i])); }
            }
            var onlyAtBank = bank.Where((t, i) => !used[i] && Inside(Day(t))).ToList();

            // Net effect, from the differences themselves: edges and posting lags add nothing
            decimal Gap(bool credit) =>
                altered.Where(x => (x.Row.Credit > 0) == credit).Sum(x => Amount(x.Row) - x.Bank.Amount)
              + onlyOnPdf.Where(r => (r.Credit > 0) == credit).Sum(Amount)
              - onlyAtBank.Where(t => t.IsCredit == credit).Sum(t => t.Amount);

            return new BankMatch(from, to, judged, judged - unmatched.Count, altered, onlyOnPdf, onlyAtBank, Gap(true), Gap(false));
        }

        // ── helpers ─────────────────────────────────────────────────────────────────

        private static decimal Amount(StatementRow r) => r.Credit is decimal c and > 0 ? c : r.Debit ?? 0;
        private static string Side(StatementRow r) => r.Credit > 0 ? "credit" : "debit";

        private static string? LastFour(string? s)
        {
            var digits = new string((s ?? "").Where(char.IsDigit).ToArray());
            return digits.Length >= 4 ? digits[^4..] : null;
        }

        private static Finding New(CaseDocument doc, string type, FindingCategory category, FindingSeverity severity,
                                   string title, string explanation, string action, IEnumerable<Evidence> evidence)
            => new()
            {
                LoanApplicationID = doc.LoanApplicationID,
                Type = type,
                Category = category,
                Severity = severity,
                Title = Clip(title, 300),
                Explanation = explanation,
                RecommendedAction = action,
                DetectedBy = FindingSource.Deterministic,
                DedupKey = $"{type}:doc{doc.CaseDocumentID}",
                Evidence = evidence.Take(MaxEvidence).Select(e => { e.Note = e.Note == null ? null : Clip(e.Note, 400); return e; }).ToList()
            };

        private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

        private static Evidence RowEvidence(StatementRow r, string note) => new()
        {
            Kind = EvidenceKind.Row, RefId = r.StatementRowID, CaseDocumentID = r.CaseDocumentID,
            PageNo = r.PageNo, Box = r.Box, Note = $"row {r.RowNo}, {r.TxnDate:dd MMM yyyy}: {note}"
        };

        private static Evidence FieldEvidence(DocField f, string note) => new()
        {
            Kind = EvidenceKind.Field, RefId = f.DocFieldID, CaseDocumentID = f.CaseDocumentID,
            PageNo = f.PageNo, Box = f.Box, Note = $"{note}: {f.Value}"
        };

        private static Evidence PageEvidence(CaseDocument doc, int page, string note) => new()
        {
            Kind = EvidenceKind.Page, RefId = doc.CaseDocumentID, CaseDocumentID = doc.CaseDocumentID, PageNo = page, Note = note
        };

        private static DocField? Field(IReadOnlyList<DocField> fields, string name)
            => fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Account number when printed, else the name squeezed to letters and digits.</summary>
        private static string? Counterparty(StatementRow r)
        {
            if (!string.IsNullOrWhiteSpace(r.CounterpartyAcct)) return "acct:" + r.CounterpartyAcct.Trim();
            if (string.IsNullOrWhiteSpace(r.CounterpartyName)) return null;
            return "name:" + new string(r.CounterpartyName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        /// <summary>Last printed balance of each day.</summary>
        private static SortedDictionary<DateOnly, decimal> EndOfDayBalances(IReadOnlyList<StatementRow> rows)
        {
            var eod = new SortedDictionary<DateOnly, decimal>();
            foreach (var r in rows.Where(r => r.Balance != null)) eod[r.TxnDate] = r.Balance!.Value;
            return eod;
        }

        /// <summary>Daily average, carrying each day's closing balance forward over days with no transactions.</summary>
        private static decimal AverageBalance(SortedDictionary<DateOnly, decimal> eod, DateOnly from, DateOnly to)
        {
            decimal? current = eod.Where(kv => kv.Key < from).Select(kv => (decimal?)kv.Value).LastOrDefault();
            decimal sum = 0; int days = 0;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (eod.TryGetValue(d, out var b)) current = b;
                if (current == null) continue;
                sum += current.Value; days++;
            }
            return days == 0 ? 0 : sum / days;
        }

        private static string Rs(decimal? v) => v == null ? "—" : "₹" + v.Value.ToString("N2", InIndia);
        private static string Signed(decimal v) => v >= 0 ? $"+ {Rs(v)}" : $"− {Rs(-v)}";
        private static string Times(int n, string noun = "") => noun.Length == 0
            ? (n == 1 ? "once" : $"{n} times")
            : $"{n} {noun}{(n == 1 ? "" : "s")}";
        private static string Gap(TimeSpan t) => t.TotalDays >= 1 ? $"{t.TotalDays:0} day{(t.TotalDays >= 1.5 ? "s" : "")}"
                                               : t.TotalHours >= 1 ? $"{t.TotalHours:0} hour{(t.TotalHours >= 1.5 ? "s" : "")}"
                                               : $"{t.TotalMinutes:0} minutes";
    }
}
