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

        public static byte[] Itr(string entityName, string pan, string assessmentYear,
                                 decimal grossTotalIncome, decimal businessTurnover,
                                 decimal totalIncome, decimal taxPaid,
                                 string producer, string creator, DateTime created, DateTime modified)
            => Document.Create(c => c.Page(p =>
            {
                p.Size(PageSizes.A4);
                p.Margin(32);
                p.DefaultTextStyle(t => t.FontSize(9));

                p.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c2 =>
                        {
                            c2.Item().Text("INCOME TAX DEPARTMENT").FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                            c2.Item().Text("Government of India").FontSize(10).FontColor(Colors.Grey.Darken2);
                            c2.Item().PaddingTop(2).Text("Indian Income Tax Return Acknowledgement / Verification").Bold();
                        });
                        r.ConstantItem(120).AlignRight().Column(c2 =>
                        {
                            c2.Item().Text($"AY {assessmentYear}").FontSize(12).Bold();
                            c2.Item().Text("FORM ITR-V").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    col.Item().PaddingTop(10).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(6).Row(r =>
                    {
                        r.RelativeItem().Text($"Name: {entityName}").Bold();
                        r.RelativeItem().AlignRight().Text($"PAN: {pan}").Bold();
                    });
                });

                p.Content().PaddingTop(16).Column(col =>
                {
                    col.Item().Text("PART B - COMPUTATION OF TOTAL INCOME AND TAX").FontSize(10).Bold();

                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(cd =>
                        {
                            cd.ConstantColumn(40);
                            cd.RelativeColumn();
                            cd.ConstantColumn(120);
                        });

                        t.Header(h =>
                        {
                            h.Cell().BorderBottom(1).PaddingVertical(3).Text("No.").Bold();
                            h.Cell().BorderBottom(1).PaddingVertical(3).Text("Particulars").Bold();
                            h.Cell().BorderBottom(1).PaddingVertical(3).AlignRight().Text("Amount (₹)").Bold();
                        });

                        void AddRow(int no, string desc, decimal amount)
                        {
                            t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(no.ToString());
                            t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).Text(desc);
                            t.Cell().BorderBottom(0.25f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignRight().Text(Inr(amount));
                        }

                        AddRow(1, "Gross Turnover / Revenue from Operations", businessTurnover);
                        AddRow(2, "Gross Total Income (GTI)", grossTotalIncome);
                        AddRow(3, "Total Taxable Income", totalIncome);
                        AddRow(4, "Total Taxes Paid (Advance Tax + TDS + Self Assessment)", taxPaid);
                    });

                    col.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(box =>
                    {
                        box.Item().Text("VERIFICATION SUMMARY").Bold().FontSize(8.5f);
                        box.Item().PaddingTop(4).Text($"This acknowledgement is electronically generated for Assessment Year {assessmentYear}. "
                            + $"All figures stated above reflect the taxpayer's return submission under the Income-tax Act, 1961.").FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });

                p.Footer().Column(col =>
                {
                    col.Item().PaddingTop(4).Text(Synthetic).FontSize(6.5f).FontColor(Colors.Grey.Medium);
                });
            }))
            .WithMetadata(Metadata($"ITR-V Acknowledgement {assessmentYear}", producer, creator, created, modified))
            .GeneratePdf();

        private static DocumentMetadata Metadata(string title, string producer, string creator, DateTime created, DateTime modified) => new()
        {
            Title = title,
            Producer = producer,
            Creator = creator,
            CreationDate = ToOffset(created),
            ModifiedDate = ToOffset(modified)
        };

        private static DateTimeOffset ToOffset(DateTime dt)
            => dt.Kind == DateTimeKind.Utc
                ? new DateTimeOffset(dt).ToOffset(Ist)
                : new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Unspecified), Ist);
    }
}
