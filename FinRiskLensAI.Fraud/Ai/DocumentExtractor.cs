using System.Globalization;
using System.Text.Json.Nodes;
using FinRiskLensAI.Core.Models.Cases;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace FinRiskLensAI.Fraud.Ai
{
    /// <summary>
    /// Step ② of the case pipeline: Gemini Flash reads the PDF or image natively and returns
    /// the <see cref="ExtractedDocument"/> contract. Runs in Mumbai (Gemini:ExtractLocation).
    /// </summary>
    public class DocumentExtractor
    {
        private const string Instruction = """
            You extract data from documents submitted with a loan application, for a fraud investigation.

            Copy every number exactly as printed. Never correct, recompute or reconcile anything: a
            document whose arithmetic does not add up is precisely what the investigation needs to see.

            - Dates as YYYY-MM-DD. Amounts as plain numbers: no currency symbol, no thousands
              separators ("1,20,000.50" is 120000.50). Use null for a value that is not printed.
            - docType: BankStatement, SalarySlip, Itr, GstReturn, KycId, UtilityBill, Invoice, or
              Unclassified when none fits.
            - claimedIssuer: the bank, employer or authority the document says it comes from.
            - Bank statements: one entry in pages[] per page, with that page's transaction rows in
              reading order and its printed opening balance, closing balance and page totals when
              they are printed. A row has either a debit or a credit, never both. counterpartyName
              and counterpartyAccount: the other party as written in the narration, when present.
              Leave fields[] empty.
            - Every other document: leave pages[] empty and fill fields[], one per value, with these
              names where they apply — employee_name, employer, month, gross, total_deductions or one
              deduction_<name> per deduction line (deduction_pf, deduction_pt, deduction_tds…), net,
              pan_masked, dob, address, assessment_year, gross_total_income, business_turnover,
              total_income, tax_paid, id_type, id_number_masked. numValue for every amount.
            - layout: the template only — header position, logo, sections, table columns, fonts,
              footer. No names, numbers, dates or amounts.
            """;

        private readonly GeminiClient _gemini;

        public DocumentExtractor(GeminiClient gemini) => _gemini = gemini;

        // Shared across every extraction in the process, so a burst of uploads cannot exhaust the model quota
        private static readonly SemaphoreSlim Gate = new(6);

        /// <summary>
        /// Multi-page PDFs are split and every page read in parallel, then merged (reference design §6):
        /// a long single answer drops rows and runs into the output cap, and a dropped row is
        /// worse than a slow one — it breaks the arithmetic and reads as tampering.
        /// </summary>
        public async Task<ExtractedDocument> ExtractAsync(byte[] content, string contentType, CancellationToken ct = default)
        {
            var pages = contentType == "application/pdf" ? SplitPages(content) : null;
            if (pages == null) return Map(await ReadAsync(content, contentType, null, ct));

            var reads = await Task.WhenAll(pages.Select((page, i) => ReadAsync(page, contentType, (i + 1, pages.Count), ct)));
            var merged = Map(new RawDocument
            {
                DocType = reads.Select(r => r.DocType).Where(t => t != null && t != nameof(CaseDocType.Unclassified))
                               .GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault(),
                ClaimedIssuer = reads.Select(r => r.ClaimedIssuer).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i)),
                Layout = reads[0].Layout,
                // Each read saw one page: whatever page structure it returned is that page
                Pages = reads.Select((r, i) => new RawPage
                {
                    PageNo = i + 1,
                    OpeningBalance = r.Pages?.FirstOrDefault()?.OpeningBalance,
                    ClosingBalance = r.Pages?.LastOrDefault()?.ClosingBalance,
                    StatedTotalDebit = r.Pages?.FirstOrDefault()?.StatedTotalDebit,
                    StatedTotalCredit = r.Pages?.FirstOrDefault()?.StatedTotalCredit,
                    Rows = r.Pages?.SelectMany(p => p.Rows ?? new()).ToList()
                }).Where(p => p.Rows is { Count: > 0 } || p.OpeningBalance != null).ToList(),
                Fields = reads.SelectMany((r, i) => (r.Fields ?? new()).Select(f => { f.PageNo = i + 1; return f; }))
                              .GroupBy(f => f.Name?.Trim().ToLowerInvariant()).Select(g => g.First()).ToList()
            });
            return merged;
        }

        private async Task<RawDocument> ReadAsync(byte[] content, string contentType, (int Page, int Of)? page, CancellationToken ct)
        {
            var o = _gemini.Options;
            var ask = page == null ? "Extract this document."
                                   : $"This is page {page.Value.Page} of {page.Value.Of} of one document. Extract this page.";
            await Gate.WaitAsync(ct);
            try
            {
                return await _gemini.GenerateJsonAsync<RawDocument>(o.ExtractModel, o.ExtractLocation, Instruction,
                    new JsonNode[] { GeminiClient.Inline(content, contentType), GeminiClient.Text(ask) },
                    Schema(), maxOutputTokens: 32_000, o.ExtractThinkingLevel, ct);
            }
            finally { Gate.Release(); }
        }

        /// <summary>One single-page PDF per page; null for one-page files and for PDFs PdfPig cannot open (encrypted).</summary>
        private static List<byte[]>? SplitPages(byte[] pdf)
        {
            try
            {
                using var source = PdfDocument.Open(pdf);
                if (source.NumberOfPages <= 1) return null;
                var pages = new List<byte[]>(source.NumberOfPages);
                for (int i = 1; i <= source.NumberOfPages; i++)
                {
                    var builder = new PdfDocumentBuilder();
                    builder.AddPage(source, i);
                    pages.Add(builder.Build());
                }
                return pages;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Model output is untrusted: unknown doc types become Unclassified, undatable rows are dropped.</summary>
        public static ExtractedDocument Map(RawDocument raw) => new()
        {
            DocType = Enum.TryParse<CaseDocType>(raw.DocType, ignoreCase: true, out var t) ? t : CaseDocType.Unclassified,
            ClaimedIssuer = raw.ClaimedIssuer,
            LayoutText = raw.Layout,
            Pages = (raw.Pages ?? new()).Select(p => new ExtractedPage
            {
                PageNo = p.PageNo,
                OpeningBalance = p.OpeningBalance,
                ClosingBalance = p.ClosingBalance,
                StatedTotalDebit = p.StatedTotalDebit,
                StatedTotalCredit = p.StatedTotalCredit,
                Rows = (p.Rows ?? new())
                    .Where(r => DateOnly.TryParseExact(r.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    .Select(r => new ExtractedRow
                    {
                        Date = DateOnly.ParseExact(r.Date!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                        Description = r.Description ?? string.Empty,
                        Debit = r.Debit, Credit = r.Credit, Balance = r.Balance,
                        CounterpartyName = r.CounterpartyName, CounterpartyAccount = r.CounterpartyAccount
                    }).ToList()
            }).ToList(),
            Fields = (raw.Fields ?? new()).Where(f => !string.IsNullOrWhiteSpace(f.Name)).Select(f => new ExtractedField
            {
                Name = f.Name!.Trim().ToLowerInvariant(),
                Value = f.Value ?? string.Empty,
                NumValue = f.NumValue,
                PageNo = f.PageNo ?? 1
            }).ToList()
        };

        private static JsonObject Schema()
        {
            static JsonObject Str(bool nullable = false) => new() { ["type"] = "STRING", ["nullable"] = nullable };
            static JsonObject Num() => new() { ["type"] = "NUMBER", ["nullable"] = true };
            static JsonObject Int() => new() { ["type"] = "INTEGER" };
            static JsonArray Req(params string[] names) => new(names.Select(n => (JsonNode)n).ToArray());

            return new JsonObject
            {
                ["type"] = "OBJECT",
                ["required"] = Req("docType", "layout", "pages", "fields"),
                ["properties"] = new JsonObject
                {
                    ["docType"] = new JsonObject
                    {
                        ["type"] = "STRING",
                        ["enum"] = new JsonArray(Enum.GetNames<CaseDocType>().Select(n => (JsonNode)n).ToArray())
                    },
                    ["claimedIssuer"] = Str(nullable: true),
                    ["layout"] = Str(),
                    ["pages"] = new JsonObject
                    {
                        ["type"] = "ARRAY",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["required"] = Req("pageNo", "rows"),
                            ["properties"] = new JsonObject
                            {
                                ["pageNo"] = Int(),
                                ["openingBalance"] = Num(),
                                ["closingBalance"] = Num(),
                                ["statedTotalDebit"] = Num(),
                                ["statedTotalCredit"] = Num(),
                                ["rows"] = new JsonObject
                                {
                                    ["type"] = "ARRAY",
                                    ["items"] = new JsonObject
                                    {
                                        ["type"] = "OBJECT",
                                        ["required"] = Req("date", "description"),
                                        ["properties"] = new JsonObject
                                        {
                                            ["date"] = Str(),
                                            ["description"] = Str(),
                                            ["debit"] = Num(),
                                            ["credit"] = Num(),
                                            ["balance"] = Num(),
                                            ["counterpartyName"] = Str(nullable: true),
                                            ["counterpartyAccount"] = Str(nullable: true)
                                        }
                                    }
                                }
                            }
                        }
                    },
                    ["fields"] = new JsonObject
                    {
                        ["type"] = "ARRAY",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["required"] = Req("name", "value"),
                            ["properties"] = new JsonObject
                            {
                                ["name"] = Str(),
                                ["value"] = Str(),
                                ["numValue"] = Num(),
                                ["pageNo"] = Int()
                            }
                        }
                    }
                }
            };
        }

        // Wire shapes — everything nullable, because model output is never trusted to match.
        public sealed class RawDocument
        {
            public string? DocType { get; set; }
            public string? ClaimedIssuer { get; set; }
            public string? Layout { get; set; }
            public List<RawPage>? Pages { get; set; }
            public List<RawField>? Fields { get; set; }
        }

        public sealed class RawPage
        {
            public int PageNo { get; set; }
            public decimal? OpeningBalance { get; set; }
            public decimal? ClosingBalance { get; set; }
            public decimal? StatedTotalDebit { get; set; }
            public decimal? StatedTotalCredit { get; set; }
            public List<RawRow>? Rows { get; set; }
        }

        public sealed class RawRow
        {
            public string? Date { get; set; }
            public string? Description { get; set; }
            public decimal? Debit { get; set; }
            public decimal? Credit { get; set; }
            public decimal? Balance { get; set; }
            public string? CounterpartyName { get; set; }
            public string? CounterpartyAccount { get; set; }
        }

        public sealed class RawField
        {
            public string? Name { get; set; }
            public string? Value { get; set; }
            public decimal? NumValue { get; set; }
            public int? PageNo { get; set; }
        }
    }
}
