using System.Globalization;
using FinRiskLensAI.Core.Models.Cases;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinRiskLensAI.DemoData
{
    /// <summary>
    /// Draws the truth as a PDF. Every printed number comes from the truth document, so
    /// what the extractor should read and what the page shows can never drift apart.
    /// </summary>
    public static class Render
    {
        private const string Synthetic = "SYNTHETIC DOCUMENT - FinRiskLens demo data - not issued by any bank or employer";
        private static readonly CultureInfo InIndia = new("en-IN");
        private static readonly TimeSpan Ist = TimeSpan.FromHours(5.5);

        // Community licence: free for this use. Set here so every caller — the tool and the tests — gets it.
        static Render() => QuestPDF.Settings.License = LicenseType.Community;

        public static string Inr(decimal v) => v.ToString("N2", InIndia);
        private static string Inr(decimal? v) => v is { } x ? Inr(x) : "";

        public static byte[] Statement(Bank bank, string holder, string account, ExtractedDocument doc,
                                       string producer, string creator, DateTime created, DateTime modified)
        {
            var pages = doc.Pages.Count;
            return Document.Create(c =>
            {
                // One PDF page per truth page, so page numbers in evidence match the file
                foreach (var page in doc.Pages)
                {
                    c.Page(p =>
                    {
                        p.Size(PageSizes.A4);
                        p.Margin(28);
                        p.DefaultTextStyle(t => t.FontSize(8));

                        p.Header().Column(col =>
                        {
                            col.Item().Row(r =>
                            {
                                r.RelativeItem().Text(bank.Name).FontSize(15).Bold().FontColor(Colors.Blue.Darken3);
                                r.RelativeItem().AlignRight().Text($"{bank.Branch}\nIFSC {bank.Ifsc}");
                            });
                            col.Item().PaddingTop(6).Text($"Statement of account: {holder}").Bold();
                            col.Item().Text($"Account {account}   Period 01-06-2026 to 30-09-2026");
                            col.Item().PaddingTop(4).Text($"Opening balance: {Inr(page.OpeningBalance)}");
                        });

                        p.Content().PaddingTop(8).Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.ConstantColumn(56);
                                cd.RelativeColumn();
                                cd.ConstantColumn(72);
                                cd.ConstantColumn(72);
                                cd.ConstantColumn(80);
                            });
                            t.Header(h =>
                            {
                                foreach (var label in new[] { "Date", "Narration", "Debit", "Credit", "Balance" })
                                    h.Cell().BorderBottom(1).PaddingVertical(3)
                                     .AlignLeft().Text(label).Bold();
                            });
                            foreach (var r in page.Rows)
                            {
                                t.Cell().BorderBottom(0.25f).PaddingVertical(2.5f).Text(r.Date.ToString("dd-MM-yyyy"));
                                t.Cell().BorderBottom(0.25f).PaddingVertical(2.5f).Text(r.Description);
                                t.Cell().BorderBottom(0.25f).PaddingVertical(2.5f).AlignRight().Text(Inr(r.Debit));
                                t.Cell().BorderBottom(0.25f).PaddingVertical(2.5f).AlignRight().Text(Inr(r.Credit));
                                t.Cell().BorderBottom(0.25f).PaddingVertical(2.5f).AlignRight().Text(Inr(r.Balance));
                            }
                        });

                        p.Footer().Column(col =>
                        {
                            col.Item().Text($"Page totals: Debit {Inr(page.StatedTotalDebit)}   Credit {Inr(page.StatedTotalCredit)}   Closing balance {Inr(page.ClosingBalance)}").Bold();
                            col.Item().PaddingTop(3).Row(r =>
                            {
                                r.RelativeItem().Text(Synthetic).FontSize(6.5f).FontColor(Colors.Grey.Medium);
                                r.ConstantItem(60).AlignRight().Text($"Page {page.PageNo} of {pages}").FontSize(7);
                            });
                        });
                    });
                }
            })
            .WithMetadata(Metadata($"{bank.Name} statement", producer, creator, created, modified))
            .GeneratePdf();
        }

        public static byte[] Slip(string employer, string employee, string month, decimal gross, decimal pf, decimal pt,
                                  decimal tds, decimal net, string producer, string creator, DateTime created, DateTime modified)
            => Document.Create(c => c.Page(p =>
            {
                p.Size(PageSizes.A5.Landscape());
                p.Margin(26);
                p.DefaultTextStyle(t => t.FontSize(9));

                p.Header().Column(col =>
                {
                    col.Item().Text(employer).FontSize(14).Bold();
                    col.Item().Text($"Payslip for {month}");
                    col.Item().PaddingTop(4).Text($"Employee: {employee}");
                });

                p.Content().PaddingTop(10).Row(r =>
                {
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().BorderBottom(1).Text("Earnings").Bold();
                        col.Item().Row(x => { x.RelativeItem().Text("Gross salary"); x.RelativeItem().AlignRight().Text(Inr(gross)); });
                    });
                    r.ConstantItem(24);
                    r.RelativeItem().Column(col =>
                    {
                        col.Item().BorderBottom(1).Text("Deductions").Bold();
                        col.Item().Row(x => { x.RelativeItem().Text("Provident fund"); x.RelativeItem().AlignRight().Text(Inr(pf)); });
                        col.Item().Row(x => { x.RelativeItem().Text("Professional tax"); x.RelativeItem().AlignRight().Text(Inr(pt)); });
                        col.Item().Row(x => { x.RelativeItem().Text("Income tax (TDS)"); x.RelativeItem().AlignRight().Text(Inr(tds)); });
                    });
                });

                p.Footer().Column(col =>
                {
                    col.Item().Border(1).Padding(6).Row(x =>
                    {
                        x.RelativeItem().Text("Net pay").Bold();
                        x.RelativeItem().AlignRight().Text(Inr(net)).Bold();
                    });
                    col.Item().PaddingTop(4).Text(Synthetic).FontSize(6.5f).FontColor(Colors.Grey.Medium);
                });
            }))
            .WithMetadata(Metadata($"Payslip {month}", producer, creator, created, modified))
            .GeneratePdf();

        private static DocumentMetadata Metadata(string title, string producer, string creator, DateTime created, DateTime modified) => new()
        {
            Title = title,
            Producer = producer,
            Creator = creator,
            CreationDate = new DateTimeOffset(created, Ist),
            ModifiedDate = new DateTimeOffset(modified, Ist)
        };
    }
}
