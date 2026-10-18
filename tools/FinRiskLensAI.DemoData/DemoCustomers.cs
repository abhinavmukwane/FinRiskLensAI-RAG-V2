using System.Globalization;
using System.Text.Json;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.User_Activity;
using FinRiskLensAI.Data.DbContextEDMX;
using FinRiskLensAI.Data.Repositories.Cases;
using FinRiskLensAI.Fraud.Cases;
using FinRiskLensAI.Fraud.Checks;
using FinRiskLensAI.Fraud.Storage;
using FinRiskLensAI.ML.Features;
using FinRiskLensAI.ML.Services;
using FinRiskLensAI.Services.Implementation.AccountAggregator;
using FinRiskLensAI.Services.Implementation.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinRiskLensAI.DemoData
{
    public sealed record DemoCustomerSpec(
        string Uan,
        string EnterpriseName,
        string Pan,
        string Gstin,
        string Email,
        string Mobile,
        string Activity,
        string OrgType,
        string City,
        string State,
        string Pin,
        string Story,
        string[] ExpectedFindings,
        RiskVerdict ExpectedVerdict,
        decimal MonthlyTurnover,
        decimal AnnualIncome,
        string? SharedDin = null,
        string? InvoicedGstin = null,
        bool IsPassThrough = false,
        bool DisagreeAaWithUpload = false,
        bool TamperUploadedItr = false,
        decimal InflatedBankCredits = 0
    );

    /// <summary>
    /// Task T11 — Demo Customers CLI Generator (Google Cloud AI Builder Cup 2026).
    /// Generates structured JSON source files customer-wise and uploads them to the
    /// configured storage bucket (gs://finrisklensai-sources/{UAN}/) or a local export folder.
    /// Seeds customer registration in the database and verifies expected findings.
    /// </summary>
    public static class DemoCustomers
    {
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static List<DemoCustomerSpec> All()
        {
            return new List<DemoCustomerSpec>
            {
                // 1. Clear: Consistent everywhere -> Clear -> Approve
                new(
                    Uan: "UDYAM-MH-99-0000301",
                    EnterpriseName: "Sankalp Autotech Private Limited",
                    Pan: "AAACS0301K",
                    Gstin: "27AAACS0301K1Z1",
                    Email: "sankalp0301@finrisklens.demo",
                    Mobile: "9823714401",
                    Activity: "Manufacturing",
                    OrgType: "Private Limited Company",
                    City: "Nashik",
                    State: "Maharashtra",
                    Pin: "422007",
                    Story: "Genuine auto component manufacturer; consistent filings across GST, ITR, AA, and Udyam.",
                    ExpectedFindings: Array.Empty<string>(),
                    ExpectedVerdict: RiskVerdict.Clear,
                    MonthlyTurnover: 1200000m,
                    AnnualIncome: 2400000m
                ),

                // 2. Edited Statement: Uploaded PDF disagrees with raw AA record
                new(
                    Uan: "UDYAM-MH-99-0000302",
                    EnterpriseName: "Mahalaxmi Agro Implements Private Limited",
                    Pan: "AAACM0302K",
                    Gstin: "27AAACM0302K1Z5",
                    Email: "mahalaxmi.agro@finrisklens.demo",
                    Mobile: "9823714403",
                    Activity: "Manufacturing",
                    OrgType: "Private Limited Company",
                    City: "Pune",
                    State: "Maharashtra",
                    Pin: "411014",
                    Story: "Uploaded bank statement inflated deposits and deleted debt EMIs vs bank's AA record.",
                    ExpectedFindings: new[] { "STATEMENT_VS_AA_MISMATCH" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 850000m,
                    AnnualIncome: 1800000m,
                    DisagreeAaWithUpload: true
                ),

                // 3. Turnover Inflation: Bank credits 3x higher than GST & ITR
                new(
                    Uan: "UDYAM-MH-99-0000303",
                    EnterpriseName: "Apex Trading Corporation Private Limited",
                    Pan: "AAACA0303L",
                    Gstin: "27AAACA0303L1Z6",
                    Email: "apex.trading@finrisklens.demo",
                    Mobile: "9823714404",
                    Activity: "Services",
                    OrgType: "Private Limited Company",
                    City: "Mumbai",
                    State: "Maharashtra",
                    Pin: "400051",
                    Story: "Bank credits average ₹19L/month against GST turnover of ₹6L/month and ITR turnover of ₹6L/month.",
                    ExpectedFindings: new[] { "TURNOVER_MISMATCH" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 600000m,
                    AnnualIncome: 1200000m,
                    InflatedBankCredits: 1900000m
                ),

                // 4. Edited ITR: Uploaded ITR copy turnover higher than filed return
                new(
                    Uan: "UDYAM-MH-99-0000304",
                    EnterpriseName: "Shivam Tech Enterprises Private Limited",
                    Pan: "AAACS0304M",
                    Gstin: "27AAACS0304M1Z7",
                    Email: "shivam.tech@finrisklens.demo",
                    Mobile: "9823714405",
                    Activity: "Services",
                    OrgType: "Private Limited Company",
                    City: "Nagpur",
                    State: "Maharashtra",
                    Pin: "440010",
                    Story: "Uploaded ITR claims business turnover of ₹85L whereas filed return reflects ₹45L.",
                    ExpectedFindings: new[] { "ITR_MISMATCH" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 375000m,
                    AnnualIncome: 1200000m,
                    TamperUploadedItr: true
                ),

                // 5. Mule Account: AA shows pass-through (receipts forwarded same day)
                new(
                    Uan: "UDYAM-MH-99-0000305",
                    EnterpriseName: "Sunrise Trading Co Private Limited",
                    Pan: "AAACS0305N",
                    Gstin: "27AAACS0305N1Z8",
                    Email: "sunrise.trading@finrisklens.demo",
                    Mobile: "9823714406",
                    Activity: "Services",
                    OrgType: "Private Limited Company",
                    City: "Thane",
                    State: "Maharashtra",
                    Pin: "400601",
                    Story: "Account Aggregator reveals 98% velocity pass-through with no balance retention.",
                    ExpectedFindings: new[] { "AA_PASS_THROUGH" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 900000m,
                    AnnualIncome: 1500000m,
                    IsPassThrough: true
                ),

                // 6, 7, 8. Circular Trading Ring: Invoicing loop with shared director DIN 09887766
                new(
                    Uan: "UDYAM-MH-99-0000306",
                    EnterpriseName: "Zenith Components Private Limited",
                    Pan: "AAACZ0306A",
                    Gstin: "27AAACZ0306A1Z1",
                    Email: "zenith.comp@finrisklens.demo",
                    Mobile: "9823714407",
                    Activity: "Manufacturing",
                    OrgType: "Private Limited Company",
                    City: "Aurangabad",
                    State: "Maharashtra",
                    Pin: "431005",
                    Story: "Part of a 3-way circular trading loop (invoices Coral Systems), shares DIN 09887766.",
                    ExpectedFindings: new[] { "FRAUD_RING" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 1500000m,
                    AnnualIncome: 2500000m,
                    SharedDin: "09887766",
                    InvoicedGstin: "27AAACC0307B1Z2"
                ),
                new(
                    Uan: "UDYAM-MH-99-0000307",
                    EnterpriseName: "Coral Systems Private Limited",
                    Pan: "AAACC0307B",
                    Gstin: "27AAACC0307B1Z2",
                    Email: "coral.systems@finrisklens.demo",
                    Mobile: "9823714408",
                    Activity: "Services",
                    OrgType: "Private Limited Company",
                    City: "Aurangabad",
                    State: "Maharashtra",
                    Pin: "431005",
                    Story: "Part of a 3-way circular trading loop (invoices Apex Industrial), shares DIN 09887766.",
                    ExpectedFindings: new[] { "FRAUD_RING" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 1500000m,
                    AnnualIncome: 2500000m,
                    SharedDin: "09887766",
                    InvoicedGstin: "27AAAAA0308C1Z3"
                ),
                new(
                    Uan: "UDYAM-MH-99-0000308",
                    EnterpriseName: "Apex Industrial Solutions LLP",
                    Pan: "AAAAA0308C",
                    Gstin: "27AAAAA0308C1Z3",
                    Email: "apex.industrial@finrisklens.demo",
                    Mobile: "9823714409",
                    Activity: "Manufacturing",
                    OrgType: "Limited Liability Partnership",
                    City: "Aurangabad",
                    State: "Maharashtra",
                    Pin: "431005",
                    Story: "Part of a 3-way circular trading loop (invoices Zenith Components), shares DIN 09887766.",
                    ExpectedFindings: new[] { "FRAUD_RING" },
                    ExpectedVerdict: RiskVerdict.HighRisk,
                    MonthlyTurnover: 1500000m,
                    AnnualIncome: 2500000m,
                    SharedDin: "09887766",
                    InvoicedGstin: "27AAACZ0306A1Z1"
                )
            };
        }

        public static string BuildUdyamJson(DemoCustomerSpec c)
        {
            var udyam = new
            {
                client_id = $"FRL-DUMMY-{c.Uan[^6..]}",
                uan = c.Uan,
                certificate_url = $"https://udyamregistration.gov.in/certificate/{c.Uan}.pdf",
                main_details = new
                {
                    enterprise_type_list = new[]
                    {
                        new { classification_year = "2024", enterprise_type = "Small", classification_date = "2024-04-01T00:00:00" }
                    },
                    name_of_enterprise = c.EnterpriseName,
                    major_activity = c.Activity,
                    social_category = "General",
                    date_of_commencement = "2016-06-18T00:00:00",
                    dic_name = c.City.ToUpperInvariant(),
                    state = c.State.ToUpperInvariant(),
                    applied_date = "2020-07-02T00:00:00",
                    flat = "Plot No. 42",
                    name_of_building = "MIDC Industrial Area",
                    road = "Station Road",
                    city = c.City,
                    pin = c.Pin,
                    mobile_number = c.Mobile,
                    email = c.Email,
                    organization_type = c.OrgType,
                    date_of_incorporation = "2016-06-18T00:00:00",
                    registration_date = "2020-07-02T00:00:00",
                    gstin_number = c.Gstin,
                    pan_number = c.Pan
                },
                plants = new[]
                {
                    new
                    {
                        unit_name = $"{c.EnterpriseName} - Unit 1",
                        flat = "Plot 42",
                        building = "MIDC Industrial Area",
                        road = "Station Road",
                        city = c.City,
                        pin = c.Pin,
                        state = c.State.ToUpperInvariant()
                    }
                },
                nic_codes = new[]
                {
                    new
                    {
                        nic_2_digit = "28",
                        nic_4_digit = "2811",
                        nic_5_digit = "28110",
                        activity_type = c.Activity
                    }
                }
            };
            return JsonSerializer.Serialize(udyam, JsonOpts);
        }

        public static string BuildGstTaxpayerJson(DemoCustomerSpec c)
        {
            var gst = new
            {
                client_id = $"GST-TAX-{c.Gstin}",
                gstin = c.Gstin,
                trade_name = c.EnterpriseName.Split(' ')[0] + " Enterprise",
                legal_name = c.EnterpriseName,
                pan = c.Pan,
                status = "Active",
                registration_date = "2017-07-01",
                taxpayer_type = "Regular",
                constitution_of_business = c.OrgType,
                principal_place_address = new
                {
                    bno = "Plot 42",
                    bnm = "MIDC Industrial Area",
                    st = "Station Road",
                    loc = c.City,
                    pncd = c.Pin,
                    stcd = c.State
                }
            };
            return JsonSerializer.Serialize(gst, JsonOpts);
        }

        public static string BuildGstr3bJson(DemoCustomerSpec c, string period, decimal monthlyTurnover)
        {
            decimal tax = Math.Round(monthlyTurnover * 0.18m, 2);
            var gstr3b = new
            {
                response = new
                {
                    message = new
                    {
                        data = new
                        {
                            gstin = c.Gstin,
                            ret_period = period,
                            ret_prd = period,
                            filing_date = "2026-02-18",
                            status = "Filed",
                            sup_details = new
                            {
                                osup_det = new
                                {
                                    txval = monthlyTurnover,
                                    iamt = Math.Round(tax * 0.5m, 2),
                                    camt = Math.Round(tax * 0.25m, 2),
                                    samt = Math.Round(tax * 0.25m, 2),
                                    csamt = 0.00m
                                },
                                osup_zero = new { txval = 0.00m, iamt = 0.00m },
                                osup_nil_exmp = new { txval = 0.00m }
                            },
                            itc_elg = new
                            {
                                itc_net = new
                                {
                                    iamt = Math.Round(tax * 0.45m, 2),
                                    camt = Math.Round(tax * 0.22m, 2),
                                    samt = Math.Round(tax * 0.22m, 2),
                                    csamt = 0.00m
                                }
                            },
                            tx_pmt = new
                            {
                                pdcash = new[]
                                {
                                    new
                                    {
                                        iamt = Math.Round(tax * 0.05m, 2),
                                        camt = Math.Round(tax * 0.03m, 2),
                                        samt = Math.Round(tax * 0.03m, 2),
                                        csamt = 0.00m
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(gstr3b, JsonOpts);
        }

        public static string BuildGstr1B2bJson(DemoCustomerSpec c, string period)
        {
            var buyer = c.InvoicedGstin ?? "27AABCB9999F1Z9";
            var gstr1 = new
            {
                response = new
                {
                    message = new
                    {
                        data = new
                        {
                            gstin = c.Gstin,
                            fp = period,
                            b2b = new[]
                            {
                                new
                                {
                                    ctin = buyer,
                                    inv = new[]
                                    {
                                        new
                                        {
                                            inum = $"INV-2026-{c.Uan[^4..]}-01",
                                            idt = "15-01-2026",
                                            val = Math.Round(c.MonthlyTurnover * 0.85m, 2)
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(gstr1, JsonOpts);
        }

        public static string BuildItrJson(DemoCustomerSpec c)
        {
            decimal annualTurnover = c.MonthlyTurnover * 12m;
            decimal totalIncome = Math.Round(c.AnnualIncome * 0.90m, 2);
            decimal taxPaid = Math.Round(totalIncome * 0.25m, 2);

            var itr = new
            {
                request_id = $"REQ-ITR-{c.Uan[^6..]}",
                status = "SUCCESS",
                message = "ITR details fetched successfully",
                data = new
                {
                    source_identifiers = new
                    {
                        udyam_registration_number = c.Uan,
                        gstin = c.Gstin,
                        pan = c.Pan
                    },
                    entity_details = new
                    {
                        trade_name = c.EnterpriseName,
                        company_name = c.EnterpriseName
                    },
                    filing_details = new
                    {
                        assessment_year = "2026-27",
                        financial_year = "2025-26",
                        itr_form_type = c.OrgType.Contains("LLP") ? "ITR-5" : "ITR-6",
                        filing_date = "2026-07-31",
                        acknowledgement_number = $"1234567890{c.Uan[^4..]}",
                        e_verification_status = "VERIFIED",
                        return_status = "PROCESSED"
                    },
                    income_details = new
                    {
                        gross_total_income = c.AnnualIncome,
                        profits_and_gains_of_business_or_profession = c.AnnualIncome
                    },
                    profit_and_loss_summary = new
                    {
                        revenue_from_operations = annualTurnover,
                        gross_turnover = annualTurnover,
                        net_profit_before_tax = Math.Round(c.AnnualIncome * 0.85m, 2),
                        net_profit_after_tax = Math.Round(c.AnnualIncome * 0.70m, 2)
                    },
                    gst_turnover_reconciliation = new
                    {
                        itr_declared_turnover = annualTurnover,
                        reconciliation_status = "WITHIN_TOLERANCE"
                    },
                    tax_computation = new
                    {
                        total_tax_and_interest = taxPaid,
                        taxes_paid = new
                        {
                            total_taxes_paid = taxPaid
                        }
                    },
                    total_income = totalIncome,
                    business_turnover = annualTurnover,
                    gross_total_income = c.AnnualIncome,
                    tax_paid = taxPaid,
                    assessment_year = "2026-27"
                }
            };
            return JsonSerializer.Serialize(itr, JsonOpts);
        }

        public static string BuildMcaJson(DemoCustomerSpec c)
        {
            var din = c.SharedDin ?? $"07{c.Uan[^6..]}";
            var directorName = c.SharedDin != null ? "Rajesh Kumar Sharma" : $"{c.EnterpriseName.Split(' ')[0]} Director";

            var mca = new
            {
                status = true,
                message = new
                {
                    details = new
                    {
                        cin = $"U29299MH2016PTC{c.Uan[^6..]}",
                        company_name = c.EnterpriseName,
                        company_status = "ACTIVE",
                        date_of_incorporation = "2016-06-18",
                        paid_up_capital = "5000000",
                        authorized_capital = "10000000",
                        directors = new[]
                        {
                            new
                            {
                                din_number = din,
                                director_name = directorName,
                                designation = "Director",
                                date_of_appointment = "2016-06-18",
                                end_date = (string?)null
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(mca, JsonOpts);
        }

        public static string BuildDinJson(string din, string directorName, string pan)
        {
            var dinObj = new
            {
                status = true,
                data = new
                {
                    din_number = din,
                    director_name = directorName,
                    pan_number = pan,
                    din_status = "Approved",
                    date_of_approval = "2015-05-10"
                }
            };
            return JsonSerializer.Serialize(dinObj, JsonOpts);
        }

        public static string BuildAaJson(DemoCustomerSpec c)
        {
            var from = new DateOnly(2026, 6, 1);
            var to = new DateOnly(2026, 9, 30);
            var rows = new List<(DateOnly Date, string Desc, decimal In, decimal Out, decimal Bal)>();
            decimal bal = 500000m;

            if (c.IsPassThrough)
            {
                // Mule-like pass-through: every receipt is forwarded on the same day for almost the exact amount
                for (var m = 6; m <= 9; m++)
                {
                    foreach (var day in new[] { 4, 8, 12, 16, 20, 24, 28 })
                    {
                        var d = new DateOnly(2026, m, day);
                        var creditAmt = 150000m;
                        bal += creditAmt;
                        rows.Add((d, "NEFT CR CLIENT INWARD ZENPAY", creditAmt, 0, bal));

                        var debitAmt = 148500m;
                        bal -= debitAmt;
                        rows.Add((d, "IMPS DR FORWARD WALLET ZENPAY", 0, debitAmt, bal));
                    }
                }
            }
            else
            {
                decimal monthlyTarget = c.InflatedBankCredits > 0 ? c.InflatedBankCredits : c.MonthlyTurnover;
                decimal dailyCredit = Math.Round(monthlyTarget / 10m, 2);
                decimal supplierDebit = Math.Round(dailyCredit * 0.65m, 2);

                for (var m = 6; m <= 9; m++)
                {
                    foreach (var day in new[] { 2, 5, 8, 11, 14, 17, 20, 23, 26, 29 })
                    {
                        var d = new DateOnly(2026, m, day);
                        bal += dailyCredit;
                        rows.Add((d, "NEFT CR COMMERCIAL CLIENT INVOICE", dailyCredit, 0, bal));

                        bal -= supplierDebit;
                        rows.Add((d, "RTGS DR SUPPLIER RAW MATERIALS", 0, supplierDebit, bal));
                    }

                    // Monthly overheads
                    bal -= 40000m;
                    rows.Add((new DateOnly(2026, m, 1), "NEFT DR RENT INDUSTRIAL PREMISES", 0, 40000m, bal));
                    bal -= 120000m;
                    rows.Add((new DateOnly(2026, m, 7), "SALARY PAYOUT EMPLOYEES", 0, 120000m, bal));
                    bal -= 35000m;
                    rows.Add((new DateOnly(2026, m, 20), "GST PAYMENT CBIC", 0, 35000m, bal));
                }
            }

            var orderedRows = rows.OrderBy(r => r.Date).ToList();
            bal = 500000m;
            var finalRows = new List<(DateOnly Date, string Desc, decimal In, decimal Out, decimal Bal)>();
            foreach (var r in orderedRows)
            {
                bal += r.In - r.Out;
                finalRows.Add((r.Date, r.Desc, r.In, r.Out, bal));
            }

            var aa = new
            {
                body = new[]
                {
                    new
                    {
                        fipName = "Kaveri Demo Bank",
                        fiObjects = new[]
                        {
                            new
                            {
                                maskedAccNumber = $"XXXXXXXX{c.Uan[^4..]}",
                                type = "deposit",
                                Summary = new
                                {
                                    currentBalance = finalRows[^1].Bal.ToString("0.00", CultureInfo.InvariantCulture),
                                    type = "CURRENT",
                                    status = "ACTIVE",
                                    branch = "Pune Main Branch",
                                    ifscCode = "KVDB0000412"
                                },
                                Transactions = new
                                {
                                    startDate = from.ToString("yyyy-MM-dd"),
                                    endDate = to.ToString("yyyy-MM-dd"),
                                    Transaction = finalRows.Select((r, i) => new
                                    {
                                        txnId = $"TXN{c.Uan[^4..]}{i + 1:00000}",
                                        type = r.In > 0 ? "CREDIT" : "DEBIT",
                                        mode = r.Desc.Split(' ')[0],
                                        amount = (r.In > 0 ? r.In : r.Out).ToString("0.00", CultureInfo.InvariantCulture),
                                        currentBalance = r.Bal.ToString("0.00", CultureInfo.InvariantCulture),
                                        transactionTimestamp = $"{r.Date:yyyy-MM-dd}T10:00:00",
                                        valueDate = r.Date.ToString("yyyy-MM-dd"),
                                        narration = r.Desc,
                                        reference = $"REF{i + 1:000000}"
                                    })
                                }
                            }
                        }
                    }
                }
            };
            return JsonSerializer.Serialize(aa, JsonOpts);
        }

        /// <summary>
        /// Saves all source JSON files customer-wise into the storage bucket (or local folder).
        /// </summary>
        public static async Task SaveCustomerSourcesAsync(IMsmeDataStore store, DemoCustomerSpec c, string? exportDir = null, string? customAaJson = null, CancellationToken ct = default)
        {
            var files = new Dictionary<string, string>
            {
                [MsmeDataFiles.Udyam] = BuildUdyamJson(c),
                [MsmeDataFiles.GstTaxpayer] = BuildGstTaxpayerJson(c),
                ["gstr3b_012026.json"] = BuildGstr3bJson(c, "012026", c.MonthlyTurnover),
                ["gstr3b_022026.json"] = BuildGstr3bJson(c, "022026", c.MonthlyTurnover),
                ["gstr3b_032026.json"] = BuildGstr3bJson(c, "032026", c.MonthlyTurnover),
                ["gstr1_b2b_012026.json"] = BuildGstr1B2bJson(c, "012026"),
                [MsmeDataFiles.Itr] = BuildItrJson(c),
                [MsmeDataFiles.Mca] = BuildMcaJson(c),
                [MsmeDataFiles.Aa] = customAaJson ?? BuildAaJson(c)
            };

            var din = c.SharedDin ?? $"07{c.Uan[^6..]}";
            var directorName = c.SharedDin != null ? "Rajesh Kumar Sharma" : $"{c.EnterpriseName.Split(' ')[0]} Director";
            files[MsmeDataFiles.DinFile(din)] = BuildDinJson(din, directorName, c.Pan);

            foreach (var (fileName, content) in files)
            {
                await store.UploadAsync(c.Uan, fileName, content, ct);

                if (!string.IsNullOrWhiteSpace(exportDir))
                {
                    var targetFolder = Path.Combine(exportDir, c.Uan);
                    Directory.CreateDirectory(targetFolder);
                    await File.WriteAllTextAsync(Path.Combine(targetFolder, fileName), content, ct);
                }
            }
        }

        /// <summary>
        /// Seeds database onboarding registration records for the customer so they
        /// appear on the Bank Portal's Customers list and can log into /Auth/CustLogin.
        /// </summary>
        public static async Task SeedCustomerRegistrationAsync(ApplicationDbContext db, DemoCustomerSpec c, CancellationToken ct = default)
        {
            var enq = await db.MsmeEnquiries.FirstOrDefaultAsync(x => x.Uan == c.Uan, ct);
            if (enq == null)
            {
                enq = new MsmeEnquiry
                {
                    Uan = c.Uan,
                    ClientId = $"FRL-DUMMY-{c.Uan[^6..]}",
                    NameOfEnterprise = c.EnterpriseName,
                    MajorActivity = c.Activity,
                    OrganizationType = c.OrgType,
                    State = c.State,
                    City = c.City,
                    Pin = c.Pin,
                    Email = c.Email,
                    MobileNumber = c.Mobile,
                    GstinNumber = c.Gstin,
                    PanNumber = c.Pan,
                    DateOfIncorporation = new DateTime(2016, 6, 18),
                    RegistrationDate = new DateTime(2020, 7, 2),
                    IsRegister = true,
                    Payload = BuildUdyamJson(c),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "demo-customers-cli"
                };
                db.MsmeEnquiries.Add(enq);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                enq.NameOfEnterprise = c.EnterpriseName;
                enq.MajorActivity = c.Activity;
                enq.OrganizationType = c.OrgType;
                enq.State = c.State;
                enq.City = c.City;
                enq.Pin = c.Pin;
                enq.Email = c.Email;
                enq.MobileNumber = c.Mobile;
                enq.IsRegister = true;
                enq.GstinNumber = c.Gstin;
                enq.PanNumber = c.Pan;
                enq.Payload = BuildUdyamJson(c);
                await db.SaveChangesAsync(ct);
            }

            var reg = await db.UserRegistration.FirstOrDefaultAsync(x => x.UdyamNumber == c.Uan, ct);
            if (reg == null)
            {
                reg = new UserRegistrationModel
                {
                    MsmeEnquiryID = enq.MsmeEnquiryID,
                    UdyamNumber = c.Uan,
                    Email = c.Email,
                    MobileNumber = c.Mobile,
                    PanNumber = c.Pan,
                    GstinNumber = c.Gstin,
                    IPAddress = "103.21.124.5",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "demo-customers-cli"
                };
                db.UserRegistration.Add(reg);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                reg.Email = c.Email;
                reg.MobileNumber = c.Mobile;
                reg.PanNumber = c.Pan;
                reg.GstinNumber = c.Gstin;
                await db.SaveChangesAsync(ct);
            }

            var config = ApplicationDbContextFactory.BuildConfiguration();
            var encryption = new EncryptionService(config);
            var otpRecord = await db.UserOtpModel.FirstOrDefaultAsync(x => x.MsmeEnquiryID == enq.MsmeEnquiryID || x.Email == c.Email, ct);
            if (otpRecord == null)
            {
                otpRecord = new UserOtpModel
                {
                    MsmeEnquiryID = enq.MsmeEnquiryID,
                    UserRegistrationID = reg.UserRegistrationID,
                    Email = c.Email,
                    MobileNumber = c.Mobile,
                    OTP = encryption.EncryptString("123456"),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "demo-customers-cli",
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "demo-customers-cli"
                };
                db.UserOtpModel.Add(otpRecord);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                otpRecord.Email = c.Email;
                otpRecord.MobileNumber = c.Mobile;
                otpRecord.OTP = encryption.EncryptString("123456");
                otpRecord.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }

        public static async Task<int> RunAsync(IConfiguration config, string[] args)
        {
            string? exportDir = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i].Equals("--export-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    exportDir = args[i + 1];
                    break;
                }
            }

            var sourcesBucket = config["Gcs:SourcesBucket"] ?? "finrisklensai-sources";
            var casesBucket = config["Gcs:CasesBucket"] ?? "finrisklensai-cases";

            IMsmeDataStore sourcesStore;
            ICaseDocumentStore caseDocStore;
            try
            {
                var gcsSources = new GcsMsmeDataStore(sourcesBucket);
                await gcsSources.ExistsAsync("UDYAM-MH-99-TEST", "udyam.json");
                sourcesStore = gcsSources;
                caseDocStore = new GcsDocumentStore(casesBucket);
                Console.WriteLine($"Connected to Google Cloud Storage: gs://{sourcesBucket}/ and gs://{casesBucket}/");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cloud Storage unavailable or unauthenticated ({ex.Message}); using local directory store.");
                var localSourcesDir = string.IsNullOrWhiteSpace(exportDir) ? Path.Combine("Doc", "demo-data", "sources") : exportDir;
                var localCasesDir = Path.Combine(localSourcesDir, "_cases");
                sourcesStore = new LocalDirectoryDataStore(localSourcesDir);
                caseDocStore = new LocalDocumentStore(localCasesDir);
            }

            await using var db = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
            var repo = new CaseRepository(db);
            var aaService = new AaStatementAnalysisService();
            var factsService = new SourceFactsService(
                new UdyamFeatureExtractor(),
                new ItrFeatureExtractor(),
                new GstFeatureExtractor(),
                new AaFeatureExtractor());
            var blobAnalysis = new BlobAnalysisService(
                sourcesStore,
                factsService,
                new RiskScoringService(),
                NullLogger<BlobAnalysisService>.Instance);

            var caseService = new CaseService(repo, caseDocStore, new FraudOptions(), sourcesStore, aaService, blobAnalysis);

            var demoCustomers = All();
            Console.WriteLine($"Generating structured source JSON files and onboarding {demoCustomers.Count} demo customers...");

            var assessmentIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in demoCustomers)
            {
                Console.WriteLine($"Processing {c.Uan} ({c.EnterpriseName})...");

                // Clean up previous assessment for this demo customer to make runs clean and idempotent
                var oldAppIds = await db.LoanApplications.Where(a => a.Uan == c.Uan).Select(a => a.LoanApplicationID).ToListAsync();
                if (oldAppIds.Count > 0)
                {
                    var docIds = await db.CaseDocuments.Where(d => oldAppIds.Contains(d.LoanApplicationID)).Select(d => d.CaseDocumentID).ToListAsync();
                    if (docIds.Count > 0)
                    {
                        await db.StatementRows.Where(r => docIds.Contains(r.CaseDocumentID)).ExecuteDeleteAsync();
                        await db.PageSummaries.Where(p => docIds.Contains(p.CaseDocumentID)).ExecuteDeleteAsync();
                        await db.DocFields.Where(f => docIds.Contains(f.CaseDocumentID)).ExecuteDeleteAsync();
                        await db.CaseDocuments.Where(d => docIds.Contains(d.CaseDocumentID)).ExecuteDeleteAsync();
                    }
                    var findingIds = await db.Findings.Where(f => oldAppIds.Contains(f.LoanApplicationID)).Select(f => f.FindingID).ToListAsync();
                    if (findingIds.Count > 0)
                    {
                        await db.Evidence.Where(e => findingIds.Contains(e.FindingID)).ExecuteDeleteAsync();
                        await db.Findings.Where(f => oldAppIds.Contains(f.LoanApplicationID)).ExecuteDeleteAsync();
                    }
                    await db.CreditDecisions.Where(d => oldAppIds.Contains(d.LoanApplicationID)).ExecuteDeleteAsync();
                    await db.AiDropLogs.Where(l => oldAppIds.Contains(l.LoanApplicationID)).ExecuteDeleteAsync();
                    await db.LoanApplications.Where(a => oldAppIds.Contains(a.LoanApplicationID)).ExecuteDeleteAsync();
                }

                // 1. Build document and custom AA feed if required
                var (doc, customAa) = Scenarios.BuildCustomerDoc(c);

                // 2. Save structured JSON source files customer-wise (storage bucket and optional export dir)
                await SaveCustomerSourcesAsync(sourcesStore, c, exportDir, customAa);

                // 3. Seed database onboarding records (MsmeEnquiries, UserRegistration, UserOtp)
                await SeedCustomerRegistrationAsync(db, c);

                // 4. Ensure customer's assessment exists
                var assessmentId = await caseService.EnsureAssessmentAsync(c.Uan, c.EnterpriseName, "demo-customers-cli");
                assessmentIds[c.Uan] = assessmentId;

                // 5. Attach document upload if applicable
                if (doc != null)
                {
                    var intake = await caseService.AddCustomerDocumentsAsync(c.Uan, c.EnterpriseName, new[] { new CaseUpload(doc.FileName, doc.Pdf) }, "demo-customers-cli");
                    var app = await repo.GetAsync(assessmentId);
                    foreach (var pendingDoc in app?.Documents.Where(d => d.ExtractStatus == ExtractStatus.Pending) ?? Enumerable.Empty<CaseDocument>())
                    {
                        await caseService.ApplyExtractionAsync(assessmentId, pendingDoc.CaseDocumentID, doc.Truth, "demo-customers-cli:truth");
                    }
                }

                // 6. Run checks for this assessment
                await caseService.RunChecksAsync(assessmentId);
            }

            // Refresh checks across all demo customers now that all entities exist (evaluates full network loops)
            foreach (var c in demoCustomers)
            {
                if (assessmentIds.TryGetValue(c.Uan, out var id))
                {
                    await caseService.RunChecksAsync(id);
                }
            }

            // Print expected vs actual verdicts and findings comparison table
            int mismatches = 0;
            Console.WriteLine();
            Console.WriteLine($"{"Udyam",-23} {"Customer",-32} {"Expected Verdict",-16} {"Actual Verdict",-16} {"Status",-6} Findings");
            Console.WriteLine(new string('-', 120));

            static string Trunc(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

            foreach (var c in demoCustomers)
            {
                var id = assessmentIds[c.Uan];
                var findings = await repo.GetFindingsAsync(id);
                var actualFindingTypes = findings.Select(f => f.Type).Distinct().OrderBy(t => t).ToArray();

                var app = (await repo.GetAsync(id))!;
                var actualVerdict = RiskSummary.Of(app, findings);

                var expectedTypes = c.ExpectedFindings.OrderBy(t => t).ToArray();
                bool findingsOk = actualFindingTypes.SequenceEqual(expectedTypes);
                bool verdictOk = actualVerdict == c.ExpectedVerdict;
                bool ok = findingsOk && verdictOk;
                if (!ok) mismatches++;

                var statusStr = ok ? "MATCH" : "DIFF";
                Console.WriteLine($"{c.Uan,-23} {Trunc(c.EnterpriseName, 30),-32} {c.ExpectedVerdict,-16} {actualVerdict,-16} {statusStr,-6} {(actualFindingTypes.Length == 0 ? "None" : string.Join(", ", actualFindingTypes))}");
                if (!ok)
                {
                    var missing = expectedTypes.Except(actualFindingTypes).ToArray();
                    var extra = actualFindingTypes.Except(expectedTypes).ToArray();
                    if (missing.Length > 0) Console.WriteLine($"   MISSING: {string.Join(", ", missing)}");
                    if (extra.Length > 0) Console.WriteLine($"   EXTRA:   {string.Join(", ", extra)}");
                    foreach (var f in findings)
                    {
                        Console.WriteLine($"   [{f.Type}] {f.Title}");
                        Console.WriteLine($"      {f.Explanation}");
                    }
                }
            }

            Console.WriteLine(new string('-', 120));
            Console.WriteLine(mismatches == 0
                ? "All demo customers match their expected verdicts and findings!"
                : $"{mismatches} customer(s) differ from expectation.");
            return mismatches == 0 ? 0 : 1;
        }
    }

    /// <summary>
    /// Local filesystem implementation of IMsmeDataStore for offline / local-folder customer data export.
    /// </summary>
    public class LocalDirectoryDataStore : IMsmeDataStore
    {
        private readonly string _baseDir;

        public LocalDirectoryDataStore(string baseDir)
        {
            _baseDir = baseDir;
            Directory.CreateDirectory(_baseDir);
        }

        private string FilePath(string uan, string fileName)
            => Path.Combine(_baseDir, uan.Trim().Replace('\\', '-').Replace('/', '-').ToUpperInvariant(), fileName);

        public async Task UploadAsync(string uan, string fileName, string content, CancellationToken ct = default)
        {
            var p = FilePath(uan, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            await File.WriteAllTextAsync(p, content, ct);
        }

        public async Task<string?> DownloadAsync(string uan, string fileName, CancellationToken ct = default)
        {
            var p = FilePath(uan, fileName);
            return File.Exists(p) ? await File.ReadAllTextAsync(p, ct) : null;
        }

        public Task<IReadOnlyList<string>> ListFilesAsync(string uan, CancellationToken ct = default)
        {
            var dir = Path.Combine(_baseDir, uan.Trim().Replace('\\', '-').Replace('/', '-').ToUpperInvariant());
            if (!Directory.Exists(dir)) return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
            var files = Directory.GetFiles(dir).Select(Path.GetFileName).Where(f => f != null).Cast<string>().ToList();
            return Task.FromResult<IReadOnlyList<string>>(files);
        }

        public Task<bool> ExistsAsync(string uan, string fileName, CancellationToken ct = default)
            => Task.FromResult(File.Exists(FilePath(uan, fileName)));
    }

    /// <summary>
    /// Local filesystem implementation of ICaseDocumentStore for offline / local-folder document storage.
    /// </summary>
    public class LocalDocumentStore : ICaseDocumentStore
    {
        private readonly string _dir;

        public LocalDocumentStore(string dir)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
        }

        public async Task PutAsync(string path, byte[] content, string contentType, CancellationToken ct = default)
        {
            var p = Path.Combine(_dir, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            await File.WriteAllBytesAsync(p, content, ct);
        }

        public Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
        {
            var p = Path.Combine(_dir, path.Replace('/', Path.DirectorySeparatorChar));
            return Task.FromResult<Stream?>(File.Exists(p) ? File.OpenRead(p) : null);
        }
    }
}
