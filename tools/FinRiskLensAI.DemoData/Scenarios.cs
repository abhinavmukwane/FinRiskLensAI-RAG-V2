using System.Globalization;
using System.Text.Json;
using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.DemoData
{
    /// <summary>One synthetic document: the bytes to upload and what an exact extractor would read off them.</summary>
    public sealed record DemoDoc(string FileName, byte[] Pdf, ExtractedDocument Truth);

    /// <summary>
    /// One synthetic application, with the finding types the deterministic checks should raise
    /// (<see cref="Expected"/>) and the ones the Investigator should (<see cref="ExpectedAi"/>).
    /// </summary>
    public sealed record DemoCase(string Ref, CaseProduct Product, string? Uan, decimal? DeclaredIncome, string? Employer,
                                  decimal? DeclaredEmis, string Story, string[] Expected, List<DemoDoc> Docs)
    {
        public string[] ExpectedAi { get; init; } = Array.Empty<string>();
        /// <summary>The bank's own record of the account, as Account Aggregator delivers it, when the applicant linked one.</summary>
        public string? AaJson { get; init; }
        /// <summary>The verified ITR filing from the Income Tax department (itr.json) for cross-checking uploaded ITRs.</summary>
        public string? ItrJson { get; init; }
        /// <summary>The planted ring this case belongs to; cases sharing a name must form exactly one ring.</summary>
        public string? Ring { get; init; }
    }

    /// <summary>
    /// Synthetic applicants for the demo and the extractor eval set (Doc/10_FRAUD_LENS_BUILD.md, Phase 6).
    /// Every bank and employer is fictional and every page says SYNTHETIC. Seeded, so the
    /// same run produces the same documents.
    /// </summary>
    public static class Scenarios
    {
        private static readonly DateOnly From = new(2026, 6, 1);
        private static readonly DateOnly To = new(2026, 9, 30);

        public static List<DemoCase> All()
        {
            var cases = new List<DemoCase>();

            // ── Genuine: the checks must stay silent on these ───────────────────────
            cases.Add(Sme("DEMO-G01", seed: 11, "Sankalp Autotech Pvt Ltd", "UDYAM-MH-25-0042817", Banks.Kaveri,
                          "Genuine manufacturer, steady receipts and supplier payments.", tamper: null));
            cases.Add(Sme("DEMO-G02", seed: 12, "Mehta Packaging Works", null, Banks.Narmada,
                          "Genuine packaging unit, seasonal receipts.", tamper: null));
            cases.Add(Sme("DEMO-G03", seed: 13, "Lakshmi Agro Traders", null, Banks.Kaveri,
                          "Genuine agri trader, many small receipts.", tamper: null));
            cases.Add(Salaried("DEMO-G04", seed: 14, "Ananya Rao", Banks.Narmada, gross: 92_000, tamperSlip: false,
                               "Genuine salaried applicant; slip and statement agree."));
            cases.Add(Sme("DEMO-G05", seed: 15, "Sahyadri Food Products", "UDYAM-MH-99-0000105", Banks.Kaveri,
                          "Genuine food processor with Account Aggregator linked: the uploaded statement matches the bank's " +
                          "own record, including postings that clear a day after the statement date.", tamper: null, aaLinked: true));

            // ── Tampered documents ──────────────────────────────────────────────────
            cases.Add(Sme("DEMO-T01", seed: 21, "Shivam Steel Fabricators", null, Banks.Kaveri,
                          "A receipt was inflated tenfold in a PDF editor; balances and page totals were left alone.",
                          tamper: Tamper.InflateCredit));
            cases.Add(Sme("DEMO-T02", seed: 22, "Om Sai Logistics", null, Banks.Narmada,
                          "Page 3 was replaced with a page from a healthier month.", tamper: Tamper.SplicePage));
            // The slip's inflated net pay also disagrees with the salary actually credited — a cross-document
            // finding only the Investigator can make
            cases.Add(Salaried("DEMO-T03", seed: 23, "Rohit Kulkarni", Banks.Kaveri, gross: 68_000, tamperSlip: true,
                               "Net pay on the slip was typed in higher than gross less deductions.")
                      with { ExpectedAi = new[] { "INCOME_MISMATCH" } });
            cases.Add(Sme("DEMO-T04", seed: 24, "Mahalaxmi Agro Implements", "UDYAM-MH-99-0000204", Banks.Narmada,
                          "A careful forgery: two receipts tripled and four loan EMIs deleted, then every balance and page total " +
                          "recomputed and the bank's own PDF producer kept. Only the bank's Account Aggregator record disagrees.",
                          tamper: Tamper.CleanForgery, aaLinked: true));

            // ── Behaviour the documents are honest about ────────────────────────────
            cases.Add(Sme("DEMO-B01", seed: 31, "Vighnaharta Enterprises", null, Banks.Kaveri,
                          "Large receipts from one party go straight back to it within two days.", tamper: Tamper.RoundTrip));
            cases.Add(Sme("DEMO-B02", seed: 32, "Krishna Electricals", null, Banks.Narmada,
                          "A 25-lakh deposit parked for six days just before the application.", tamper: Tamper.WindowDress));
            cases.Add(Sme("DEMO-B03", seed: 33, "Sunrise Trading Co", null, Banks.Kaveri,
                          "Mule-like account: every receipt is forwarded the same day.", tamper: Tamper.PassThrough));

            // ── Only judgement finds these: the arithmetic is clean ─────────────────
            cases.Add(Sme("DEMO-A01", seed: 41, "Ganga Fabrication Works", null, Banks.Narmada,
                          "Monthly NACH EMIs to two lenders the application never declared, and two fresh loans 48 days apart.",
                          tamper: Tamper.HiddenLoans) with { ExpectedAi = new[] { "LOAN_STACKING", "UNDECLARED_EMI" } });
            cases.Add(Salaried("DEMO-A02", seed: 42, "Vikram Shetty", Banks.Narmada, gross: 120_000, tamperSlip: false,
                               "Weekly betting and a monthly crypto purchase; the declared EMI's NACH bounced once.",
                               vices: true) with { ExpectedAi = new[] { "BOUNCES", "GAMBLING_CRYPTO_EXPOSURE" } });

            // ── Rings: each case is clean alone; only the network lens links them ──────
            // A fabricator's kit: one tool makes every statement and slip, whichever bank or employer it
            // claims, and one account pays every "salary"
            var kit = (Producer: "PDFsharp 1.50.5147 (www.pdfsharp.com)", Creator: "DocuForge Templates", Payer: "XX9917");
            foreach (var (reference, seed, name, bank, gross, employer) in new[]
            {
                ("DEMO-R01", 61, "Karan Mehta", Banks.Kaveri, 85_000m, "Zenith Demo Infotech Pvt Ltd"),
                ("DEMO-R02", 62, "Sneha Pawar", Banks.Narmada, 78_000m, "Coral Demo Systems Pvt Ltd"),
                ("DEMO-R03", 63, "Imran Shaikh", Banks.Kaveri, 92_000m, "Apex Demo Consulting LLP"),
                ("DEMO-R04", 64, "Divya Nair", Banks.Narmada, 88_000m, "Coral Demo Systems Pvt Ltd"),
            })
                cases.Add(Salaried(reference, seed, name, bank, gross, tamperSlip: false,
                                   "One of four applicants supplied by the same fabricator: every document is internally consistent, " +
                                   "but all were made by one tool and every salary comes from one account.",
                                   kit: new RingKit(employer, kit.Payer, kit.Producer, kit.Creator)) with { Ring = "kit" });

            // One statement, two applicants: the second uploaded the first one's file
            var original = Sme("DEMO-R05", seed: 65, "Bhavani Textiles", null, Banks.Kaveri,
                               "A genuine-looking statement — also submitted, byte for byte, by DEMO-R06.", tamper: null) with { Ring = "copy" };
            cases.Add(original);
            cases.Add(original with
            {
                Ref = "DEMO-R06", Employer = "Shree Balaji Garments",
                Story = "Submitted DEMO-R05's statement file as its own.",
                Docs = original.Docs.Select(d => d with { FileName = "demo-r06_statement.pdf" }).ToList()
            });

            return cases;
        }

        /// <summary>A fabricator's kit: who the slip names as employer, the account that pays, the tool that made the files.</summary>
        private sealed record RingKit(string Employer, string PayerAccount, string Producer, string Creator);

        private enum Tamper { InflateCredit, SplicePage, RoundTrip, WindowDress, PassThrough, HiddenLoans, CleanForgery }

        // ── SME current account ─────────────────────────────────────────────────────

        private static DemoCase Sme(string reference, int seed, string business, string? uan, Bank bank, string story, Tamper? tamper,
                                    bool aaLinked = false)
        {
            var rnd = new Random(seed);
            var txns = new List<Txn>();
            var customers = new[] { "Apex Auto Components", "Bharat Retail Mart", "Coastal Distributors", "Deccan Infra Projects", "Evergreen Stores" };
            var suppliers = new[] { "Jindal Raw Materials", "Patel Transport", "Shakti Power Supplies", "Om Hardware Depot" };

            for (var d = From; d <= To; d = d.AddDays(1))
            {
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                if (tamper == Tamper.PassThrough)
                {
                    if (rnd.Next(3) == 0) continue;
                    var amt = Round(rnd.Next(40_000, 160_000));
                    var from = customers[rnd.Next(customers.Length)];
                    txns.Add(Txn.In(d, $"NEFT CR {from.ToUpperInvariant()}", amt, from, Acct(rnd)));
                    txns.Add(Txn.Out(d, "IMPS DR WALLET TOPUP ZENPAY", amt - Round(rnd.Next(500, 2_000)), "Zenpay Wallet", "XX9001"));
                    continue;
                }
                if (rnd.Next(4) == 0)
                {
                    var c = customers[rnd.Next(customers.Length)];
                    txns.Add(Txn.In(d, $"NEFT CR {c.ToUpperInvariant()}", Round(rnd.Next(25_000, 180_000)), c, Acct(rnd, c)));
                }
                if (rnd.Next(5) == 0)
                {
                    var s = suppliers[rnd.Next(suppliers.Length)];
                    txns.Add(Txn.Out(d, $"RTGS DR {s.ToUpperInvariant()}", Round(rnd.Next(20_000, 120_000)), s, Acct(rnd, s)));
                }
                if (rnd.Next(3) == 0)
                    txns.Add(Txn.Out(d, "UPI DR OFFICE EXPENSES", Round(rnd.Next(500, 6_000), 10), null, null));
                if (d.Day == 1) txns.Add(Txn.Out(d, "NEFT DR RENT FACTORY SHED", 45_000, "Gokhale Estates", "XX3312"));
                if (d.Day == 7) txns.Add(Txn.Out(d, "SALARY PAYOUT STAFF", Round(rnd.Next(180_000, 220_000)), null, null));
                if (d.Day == 20) txns.Add(Txn.Out(d, "GST PAYMENT CBIC", Round(rnd.Next(30_000, 60_000)), null, null));
            }

            if (tamper == Tamper.RoundTrip)
            {
                foreach (var day in new[] { new DateOnly(2026, 7, 6), new DateOnly(2026, 8, 4), new DateOnly(2026, 9, 8) })
                {
                    var amt = Round(rnd.Next(300_000, 500_000));
                    txns.Add(Txn.In(day, "RTGS CR SHREE GANESH TRADERS", amt, "Shree Ganesh Traders", "XX4471"));
                    txns.Add(Txn.Out(day.AddDays(2), "RTGS DR SHREE GANESH TRADERS", amt - 5_000, "Shree Ganesh Traders", "XX4471"));
                }
            }
            if (tamper == Tamper.HiddenLoans)
            {
                // An older loan's EMI runs all period; a second loan lands on 3 Jul and a third on 20 Aug
                for (var m = From; m <= To; m = m.AddMonths(1))
                    txns.Add(Txn.Out(m.AddDays(4), "NACH DR SWIFT DEMO FINANCE EMI", 18_500, "Swift Demo Finance", "XX5512"));
                txns.Add(Txn.In(new DateOnly(2026, 7, 3), "LOAN DISB KREDITPAL DEMO NBFC", 300_000, "KreditPal Demo NBFC", "XX8830"));
                txns.Add(Txn.Out(new DateOnly(2026, 8, 12), "NACH DR KREDITPAL DEMO NBFC EMI", 12_200, "KreditPal Demo NBFC", "XX8830"));
                txns.Add(Txn.Out(new DateOnly(2026, 9, 12), "NACH DR KREDITPAL DEMO NBFC EMI", 12_200, "KreditPal Demo NBFC", "XX8830"));
                txns.Add(Txn.In(new DateOnly(2026, 8, 20), "LOAN DISB QUICKCASH DEMO LENDING", 200_000, "QuickCash Demo Lending", "XX4409"));
            }
            if (tamper == Tamper.CleanForgery)
                for (var m = From; m <= To; m = m.AddMonths(1))
                    txns.Add(Txn.Out(m.AddDays(4), "NACH DR SWIFT DEMO FINANCE EMI", 32_500, "Swift Demo Finance", "XX5512"));
            if (tamper == Tamper.WindowDress)
            {
                txns.Add(Txn.In(new DateOnly(2026, 9, 18), "RTGS CR NIRMAL CAPITAL SERVICES", 2_500_000, "Nirmal Capital Services", "XX6620"));
                txns.Add(Txn.Out(new DateOnly(2026, 9, 24), "RTGS DR NIRMAL CAPITAL SERVICES", 2_480_000, "Nirmal Capital Services", "XX6620"));
            }

            var opening = Round(rnd.Next(350_000, 600_000));
            var doc = Statement(bank, business, opening, txns);
            var producer = (Producer: bank.Producer, Creator: bank.Creator);
            var created = new DateTime(2026, 10, 1, 9, 12, 0);
            var modified = created;
            var account = MaskedAccount(rnd);
            var aaJson = aaLinked ? AaFeed(bank, account, doc) : null;   // the bank's record, before any edit

            switch (tamper)
            {
                case Tamper.InflateCredit:
                {
                    // One receipt ×10. Balances and the footer were not touched — exactly what a hurried edit leaves.
                    var row = doc.Pages[1].Rows.First(r => r.Credit > 50_000);
                    row.Credit *= 10;
                    producer = ("Microsoft Word for Microsoft 365", "Microsoft Word");
                    modified = created.AddDays(2).AddHours(5);
                    break;
                }
                case Tamper.SplicePage:
                {
                    // Page 3 replaced by page 3 of the same account's statement for a better quarter
                    var donor = Statement(bank, business, opening + 900_000, txns);
                    doc.Pages[2] = donor.Pages[2];
                    producer = ("iLovePDF", "iLovePDF Merge");
                    modified = created.AddDays(1);
                    break;
                }
                case Tamper.CleanForgery:
                {
                    // Every EMI deleted and July's and August's first large receipt tripled, then the statement
                    // rebuilt so every balance and page total follows; producer and dates left as the bank's
                    var rows = doc.Pages.SelectMany(p => p.Rows).ToList();
                    var boosted = new[] { 7, 8 }.Select(mo => rows.First(r => r.Date.Month == mo && r.Credit > 50_000)).ToHashSet();
                    doc = Statement(bank, business, opening, rows
                        .Where(r => !r.Description.EndsWith(" EMI"))
                        .Select(r => new Txn(r.Date, r.Description, r.Debit, boosted.Contains(r) ? r.Credit * 3 : r.Credit,
                                             r.CounterpartyName, r.CounterpartyAccount))
                        .ToList());
                    break;
                }
            }
            doc.Fields.Add(Field("account_number", account));

            var file = $"{reference.ToLowerInvariant()}_{bank.Short}_statement_jun-sep.pdf";
            var pdf = Render.Statement(bank, business, account, doc, producer.Producer, producer.Creator, created, modified);
            var receipts = txns.Where(t => t.Credit > 0).Sum(t => t.Credit!.Value) / 4;

            // Hidden-loan applicants declare no EMIs — that is the lie
            var declaredEmis = tamper is Tamper.HiddenLoans or Tamper.CleanForgery ? 0 : Round(rnd.Next(0, 40_000));
            return new DemoCase(reference, CaseProduct.SmeLoan, uan, Round(receipts), business, declaredEmis,
                                story, Expected(tamper), new List<DemoDoc> { new(file, pdf, doc) }) { AaJson = aaJson };
        }

        private static string[] Expected(Tamper? tamper) => tamper switch
        {
            Tamper.InflateCredit => new[] { "BALANCE_DISCONTINUITY", "TOTALS_MISMATCH", "METADATA_ANOMALY" },
            Tamper.SplicePage => new[] { "BALANCE_DISCONTINUITY", "PAGE_CARRY_MISMATCH", "METADATA_ANOMALY" },
            Tamper.RoundTrip => new[] { "SALARY_ROUNDTRIP" },
            Tamper.WindowDress => new[] { "PRE_APPLICATION_WINDOW_DRESSING" },
            Tamper.PassThrough => new[] { "PASS_THROUGH" },
            Tamper.CleanForgery => new[] { "STATEMENT_VS_AA_MISMATCH" },
            _ => Array.Empty<string>()
        };

        // ── Salaried applicant: statement + latest salary slip ──────────────────────

        private static DemoCase Salaried(string reference, int seed, string name, Bank bank, decimal gross, bool tamperSlip,
                                         string story, bool vices = false, RingKit? kit = null)
        {
            var rnd = new Random(seed);
            var employer = kit?.Employer ?? "Orbitron Demo Software Pvt Ltd";
            var pf = Math.Round(gross * 0.12m * 0.5m);
            var pt = 200m;
            var tds = Math.Round(gross * 0.08m);
            var net = gross - pf - pt - tds;

            var txns = new List<Txn>();
            for (var d = From; d <= To; d = d.AddDays(1))
            {
                // The payer's account is printed in the narration, as bank statements do: it is what links a ring
                var payer = kit?.PayerAccount ?? "XX7788";
                if (d.Day == 1) txns.Add(Txn.In(d, $"NEFT CR SALARY {employer.ToUpperInvariant()} A/C {payer}", net, employer, payer));
                if (d.Day == 3) txns.Add(Txn.Out(d, "NEFT DR RENT", 24_000, "Kale Properties", "XX2045"));
                if (d.Day == 5) txns.Add(Txn.Out(d, "NACH DR SIP MUTUAL FUND", 10_000, null, null));
                if (d.Day == 10) txns.Add(Txn.Out(d, "CARD BILL PAYMENT", Round(rnd.Next(8_000, 20_000)), null, null));
                if (rnd.Next(3) == 0) txns.Add(Txn.Out(d, "UPI DR GROCERY", Round(rnd.Next(150, 2_500), 10), null, null));
                if (!vices) continue;
                if (d.DayOfWeek == DayOfWeek.Saturday)
                    txns.Add(Txn.Out(d, "UPI DR DEMOBET SPORTS BETTING", Round(rnd.Next(1_500, 4_000)), "DemoBet Sports", "demobet@upi"));
                if (d.Day == 15) txns.Add(Txn.Out(d, "IMPS DR COINDEMO CRYPTO EXCHANGE", 10_000, "CoinDemo Exchange", "XX6071"));
                // The declared EMI — paid by NACH, except in August when it bounced and was presented again
                if (d.Day == 7 && d.Month != 8) txns.Add(Txn.Out(d, "NACH DR SWIFT DEMO FINANCE EMI", 9_800, "Swift Demo Finance", "XX5512"));
                if (d == new DateOnly(2026, 8, 8)) txns.Add(Txn.Out(d, "NACH RTN CHG SWIFT DEMO FINANCE INSUFFICIENT FUNDS", 590, null, null));
                if (d == new DateOnly(2026, 8, 9)) txns.Add(Txn.Out(d, "NACH DR SWIFT DEMO FINANCE EMI REPRESENTED", 9_800, "Swift Demo Finance", "XX5512"));
            }

            var statement = Statement(bank, name, Round(rnd.Next(40_000, 90_000)), txns);
            var stFile = $"{reference.ToLowerInvariant()}_{bank.Short}_statement_jun-sep.pdf";
            var account = MaskedAccount(rnd);
            statement.Fields.Add(Field("account_number", account));
            var stPdf = Render.Statement(bank, name, account, statement, kit?.Producer ?? bank.Producer, kit?.Creator ?? bank.Creator,
                                         new DateTime(2026, 10, 1, 8, 40, 0), new DateTime(2026, 10, 1, 8, 40, 0));

            var printedNet = tamperSlip ? net + 25_000 : net;
            var slip = new ExtractedDocument
            {
                DocType = CaseDocType.SalarySlip,
                ClaimedIssuer = employer,
                LayoutText = "Payslip: company name header, month line, two-column earnings and deductions table, net pay box, signature-free footer",
                Fields =
                {
                    Field("employee_name", name),
                    Field("employer", employer),
                    Field("month", "September 2026"),
                    Field("gross", gross),
                    Field("deduction_pf", pf),
                    Field("deduction_pt", pt),
                    Field("deduction_tds", tds),
                    Field("net", printedNet)
                }
            };
            var slipPdf = Render.Slip(employer, name, "September 2026", gross, pf, pt, tds, printedNet,
                                      kit?.Producer ?? (tamperSlip ? "Canva" : "Orbitron Payroll 4.2"),
                                      kit?.Creator ?? (tamperSlip ? "Canva" : "Payroll Export"),
                                      new DateTime(2026, 9, 30, 18, 0, 0),
                                      tamperSlip ? new DateTime(2026, 10, 2, 11, 30, 0) : new DateTime(2026, 9, 30, 18, 0, 0));

            return new DemoCase(reference, CaseProduct.PersonalLoan, null, tamperSlip ? printedNet : net, employer, vices ? 9_800 : 0, story,
                                tamperSlip ? new[] { "SLIP_ARITHMETIC_ERROR", "METADATA_ANOMALY" } : Array.Empty<string>(),
                                new List<DemoDoc>
                                {
                                    new(stFile, stPdf, statement),
                                    new($"{reference.ToLowerInvariant()}_payslip_sep-2026.pdf", slipPdf, slip)
                                });
        }

        // ── Shared helpers ──────────────────────────────────────────────────────────

        public sealed record Txn(DateOnly Date, string Narration, decimal? Debit, decimal? Credit, string? Party, string? Account)
        {
            public static Txn In(DateOnly d, string n, decimal amt, string? party, string? acct) => new(d, n, null, amt, party, acct);
            public static Txn Out(DateOnly d, string n, decimal amt, string? party, string? acct) => new(d, n, amt, null, party, acct);
        }

        private const int RowsPerPage = 24;

        /// <summary>Runs the balance, skips any debit the account cannot cover, and lays rows out in pages.</summary>
        private static ExtractedDocument Statement(Bank bank, string holder, decimal opening, List<Txn> txns)
        {
            var doc = new ExtractedDocument
            {
                DocType = CaseDocType.BankStatement,
                ClaimedIssuer = bank.Name,
                LayoutText = bank.Layout
            };
            var balance = opening;
            var rows = new List<ExtractedRow>();
            foreach (var t in txns.OrderBy(t => t.Date).ThenBy(t => t.Credit == null))
            {
                if (t.Debit > balance) continue;
                balance += (t.Credit ?? 0) - (t.Debit ?? 0);
                rows.Add(new ExtractedRow
                {
                    Date = t.Date, Description = t.Narration, Debit = t.Debit, Credit = t.Credit, Balance = balance,
                    CounterpartyName = t.Party, CounterpartyAccount = t.Account
                });
            }

            var carried = opening;
            for (int i = 0, page = 1; i < rows.Count; i += RowsPerPage, page++)
            {
                var chunk = rows.Skip(i).Take(RowsPerPage).ToList();
                doc.Pages.Add(new ExtractedPage
                {
                    PageNo = page,
                    OpeningBalance = carried,
                    ClosingBalance = chunk[^1].Balance,
                    StatedTotalDebit = chunk.Sum(r => r.Debit ?? 0),
                    StatedTotalCredit = chunk.Sum(r => r.Credit ?? 0),
                    Rows = chunk
                });
                carried = chunk[^1].Balance!.Value;
            }
            return doc;
        }

        /// <summary>
        /// The bank's own record of the account in the Finvu FI-data shape Account Aggregator delivers.
        /// Every seventh posting clears the day after its statement date, as real value dates do.
        /// </summary>
        private static string AaFeed(Bank bank, string account, ExtractedDocument statement)
        {
            static string Amt(decimal? v) => v?.ToString("0.00", CultureInfo.InvariantCulture) ?? "0.00";
            var rows = statement.Pages.SelectMany(p => p.Rows).ToList();
            return JsonSerializer.Serialize(new
            {
                body = new[] { new { fipName = bank.Name, fiObjects = new[] { new
                {
                    maskedAccNumber = account,
                    type = "deposit",
                    Summary = new { currentBalance = Amt(rows[^1].Balance), type = "CURRENT", status = "ACTIVE", branch = bank.Branch, ifscCode = bank.Ifsc },
                    Transactions = new
                    {
                        startDate = From.ToString("yyyy-MM-dd"), endDate = To.ToString("yyyy-MM-dd"),
                        Transaction = rows.Select((r, i) => new
                        {
                            txnId = $"T{i + 1:000000}",
                            type = r.Credit > 0 ? "CREDIT" : "DEBIT",
                            mode = r.Description.Split(' ')[0],
                            amount = Amt(r.Credit ?? r.Debit),
                            currentBalance = Amt(r.Balance),
                            transactionTimestamp = $"{r.Date:yyyy-MM-dd}T10:00:00",
                            valueDate = (i % 7 == 6 ? r.Date.AddDays(1) : r.Date).ToString("yyyy-MM-dd"),
                            narration = r.Description,
                            reference = $"REF{i + 1:000000}"
                        })
                    }
                } } } }
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        public static DemoDoc ItrDoc(string entityName, string pan, string assessmentYear,
                                     decimal grossTotalIncome, decimal businessTurnover,
                                     decimal totalIncome, decimal taxPaid,
                                     bool tampered = false, decimal? tamperedTurnover = null)
        {
            var fileName = $"itr_{assessmentYear.Replace("-", "_")}.pdf";
            var truthTurnover = tampered ? (tamperedTurnover ?? Math.Round(businessTurnover * 1.8m)) : businessTurnover;
            var truth = new ExtractedDocument
            {
                DocType = CaseDocType.Itr,
                ClaimedIssuer = "Income Tax Department",
                LayoutText = "ITR-V acknowledgement: department header, AY top right, taxpayer details, computation table, synthetic-data footer",
                Fields = new List<ExtractedField>
                {
                    Field("assessment_year", assessmentYear),
                    Field("gross_total_income", grossTotalIncome),
                    Field("business_turnover", truthTurnover),
                    Field("total_income", totalIncome),
                    Field("tax_paid", taxPaid),
                    Field("pan_masked", pan)
                }
            };

            var pdf = Render.Itr(entityName, pan, assessmentYear, grossTotalIncome, truthTurnover, totalIncome, taxPaid,
                                 tampered ? "Canva" : "Income Tax e-Filing Portal 3.0",
                                 tampered ? "Canva PDF" : "ITD e-Filing Utility",
                                 new DateTime(2026, 7, 31, 14, 20, 0),
                                 tampered ? new DateTime(2026, 8, 15, 10, 0, 0) : new DateTime(2026, 7, 31, 14, 20, 0));

            return new DemoDoc(fileName, pdf, truth);
        }

        private static ExtractedField Field(string name, string value) => new() { Name = name, Value = value };
        private static ExtractedField Field(string name, decimal value) => new() { Name = name, Value = Render.Inr(value), NumValue = value };

        private static decimal Round(decimal v, int to = 100) => Math.Round(v / to) * to;
        private static string Acct(Random rnd, string? party = null)
            => "XX" + (party == null ? rnd.Next(1000, 9999) : party.Sum(c => c) % 9000 + 1000);   // stable per party; string.GetHashCode is randomised per process
        private static string MaskedAccount(Random rnd) => $"XXXXXXXX{rnd.Next(1000, 9999)}";
    }

    public sealed record Bank(string Name, string Short, string Branch, string Ifsc, string Producer, string Creator, string Layout);

    public static class Banks
    {
        public static readonly Bank Kaveri = new("Kaveri Demo Bank", "kaveri", "Pune Camp Branch", "KVDB0000412",
            "KDB CoreStatement 7.1", "KDB e-Statement Service",
            "Bank statement: logo and bank name top left, branch block top right, account summary box, five-column table Date | Narration | Debit | Credit | Balance, page totals row, synthetic-data footer");
        public static readonly Bank Narmada = new("Narmada Demo Bank", "narmada", "Indore Main Branch", "NMDB0001107",
            "NDB Statement Engine 3.4", "NDB NetBanking",
            "Bank statement: centred bank name, customer and period lines under it, five-column table Date | Narration | Debit | Credit | Balance with ruled rows, opening and closing balance line per page, synthetic-data footer");
    }
}

namespace FinRiskLensAI.DemoData
{
    using FinRiskLensAI.Fraud.Cases;
    using FinRiskLensAI.Fraud.Checks;
    using FinRiskLensAI.Services.Implementation.AccountAggregator;

    public static class DryRun
    {
        /// <summary>
        /// The demo cases as the network lens sees them: file hashes, PDF metadata, claimed issuers, salary credits —
        /// from the given extraction when there is one, else from the truth the documents were drawn from.
        /// </summary>
        public static IReadOnlyList<NetworkApplication> Network(IReadOnlyList<DemoCase> cases,
                                                                IReadOnlyDictionary<string, ExtractedDocument>? extracted = null)
        {
            int appId = 0, docId = 0;
            long rowId = 0;
            return cases.Select(c =>
            {
                var id = ++appId;
                var docs = new List<CaseDocument>();
                var salary = new List<StatementRow>();
                foreach (var d in c.Docs)
                {
                    var meta = DocumentInspector.ReadPdf(d.Pdf);
                    var read = extracted?.GetValueOrDefault(d.FileName) ?? d.Truth;
                    docs.Add(new CaseDocument
                    {
                        CaseDocumentID = ++docId, LoanApplicationID = id, FileName = d.FileName,
                        Sha256 = System.Security.Cryptography.SHA256.HashData(d.Pdf), DocType = read.DocType,
                        PdfProducer = meta.Producer, PdfCreator = meta.Creator, FontSet = meta.FontSet, ClaimedIssuer = read.ClaimedIssuer
                    });
                    var rowNo = 0;
                    foreach (var p in read.Pages)
                    foreach (var r in p.Rows)
                    {
                        rowNo++;
                        if (r.Credit > 0 && r.CounterpartyAccount != null && r.Description.Contains("SALARY", StringComparison.OrdinalIgnoreCase))
                            salary.Add(new StatementRow
                            {
                                StatementRowID = ++rowId, CaseDocumentID = docId, PageNo = p.PageNo, RowNo = rowNo, TxnDate = r.Date,
                                Description = r.Description, Credit = r.Credit, CounterpartyName = r.CounterpartyName, CounterpartyAcct = r.CounterpartyAccount
                            });
                    }
                }
                return new NetworkApplication(id, c.Ref, c.Employer, docs, salary);
            }).ToList();
        }

        /// <summary>
        /// The case through the checks with no database: metadata read back from the
        /// generated PDF, truth mapped to rows the way <c>CaseRepository.SaveExtractionAsync</c> does.
        /// </summary>
        public static string[] FindingTypes(DemoCase c, FraudOptions options,
                                            IReadOnlyDictionary<string, ExtractedDocument>? extracted = null)
        {
            var app = new LoanApplication { LoanApplicationID = 1, SubmittedAt = DateTime.UtcNow };
            var rows = new List<StatementRow>();
            var pages = new List<PageSummary>();
            var fields = new List<DocField>();
            int docId = 0;
            long rowId = 0, fieldId = 0;

            foreach (var d in c.Docs)
            {
                docId++;
                var meta = DocumentInspector.ReadPdf(d.Pdf);
                app.Documents.Add(new CaseDocument
                {
                    CaseDocumentID = docId, LoanApplicationID = 1, FileName = d.FileName, ContentType = "application/pdf",
                    DocType = (extracted?.GetValueOrDefault(d.FileName) ?? d.Truth).DocType, PageCount = meta.PageCount, PdfProducer = meta.Producer, PdfCreator = meta.Creator,
                    PdfCreatedAt = meta.CreatedAt, PdfModifiedAt = meta.ModifiedAt, FontSet = meta.FontSet
                });

                int rowNo = 0;
                // The extraction under test when given, else the truth the document was drawn from
                var source = extracted?.GetValueOrDefault(d.FileName) ?? d.Truth;
                foreach (var p in source.Pages)
                {
                    pages.Add(new PageSummary
                    {
                        CaseDocumentID = docId, PageNo = p.PageNo, OpeningBalance = p.OpeningBalance, ClosingBalance = p.ClosingBalance,
                        StatedTotalDebit = p.StatedTotalDebit, StatedTotalCredit = p.StatedTotalCredit
                    });
                    rows.AddRange(p.Rows.Select(r => new StatementRow
                    {
                        StatementRowID = ++rowId, CaseDocumentID = docId, PageNo = p.PageNo, RowNo = ++rowNo, TxnDate = r.Date,
                        Description = r.Description, Debit = r.Debit, Credit = r.Credit, Balance = r.Balance,
                        CounterpartyName = r.CounterpartyName, CounterpartyAcct = r.CounterpartyAccount
                    }));
                }
                fields.AddRange(source.Fields.Select(f => new DocField
                {
                    DocFieldID = ++fieldId, CaseDocumentID = docId, Name = f.Name, Value = f.Value, NumValue = f.NumValue, PageNo = f.PageNo
                }));
            }

            var bankRecord = c.AaJson == null ? null : new AaStatementAnalysisService().Analyze(c.AaJson).Transactions;
            return DeterministicChecks.Run(new CaseData(app, rows, pages, fields, bankRecord, c.ItrJson), options)
                                      .Select(f => f.Type).Distinct().OrderBy(t => t).ToArray();
        }
    }
}
