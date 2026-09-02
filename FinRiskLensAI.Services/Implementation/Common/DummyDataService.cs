using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Onboarding;
using Newtonsoft.Json;

namespace FinRiskLensAI.Services.Implementation.Common
{
    /// <summary>
    /// Generates random-but-valid dummy payloads for demos / onboarding when the
    /// real government APIs aren't wired up. Swap out per source as APIs go live.
    /// </summary>
    public class DummyDataService : IDummyDataService
    {
        // GST state code + name keyed by the 2-letter Udyam state token (UDYAM-MH-...).
        private static readonly Dictionary<string, (string Code, string Name)> _states = new()
        {
            ["MH"] = ("27", "Maharashtra"), ["KA"] = ("29", "Karnataka"), ["DL"] = ("07", "Delhi"),
            ["GJ"] = ("24", "Gujarat"), ["TN"] = ("33", "Tamil Nadu"), ["UP"] = ("09", "Uttar Pradesh"),
            ["RJ"] = ("08", "Rajasthan"), ["WB"] = ("19", "West Bengal"), ["TS"] = ("36", "Telangana"),
            ["HR"] = ("06", "Haryana"), ["MP"] = ("23", "Madhya Pradesh"), ["PB"] = ("03", "Punjab"),
        };

        /// <summary>
        /// Builds a random-but-valid dummy Udyam response for a UAN — valid PAN,
        /// GSTIN (real checksum), mobile, dates — as a JSON string in the same
        /// shape as the real API, so SaveMsmeData / the blob pipeline accept it.
        /// </summary>
        public string GetDummyUdyam(string uan)
        {
            var rng = Random.Shared;
            string L(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('A' + rng.Next(26))).ToArray());
            string D(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('0' + rng.Next(10))).ToArray());

            var stateTok = (uan.Split('-').ElementAtOrDefault(1) ?? "MH").ToUpperInvariant();
            var (stateCode, stateName) = _states.TryGetValue(stateTok, out var s) ? s : ("27", "Maharashtra");

            var names = new[] { "Sharma", "Patel", "Reddy", "Gupta", "Iyer", "Nair", "Verma" };
            var kinds = new[] { "Textiles", "Traders", "Enterprises", "Industries", "Agro", "Exports" };
            var surname = names[rng.Next(names.Length)];
            var enterprise = $"{surname} {kinds[rng.Next(kinds.Length)]} Pvt Ltd";

            var pan = $"{L(3)}C{surname[0]}{D(4)}{L(1)}";   // 4th char C = company
            var gstin = BuildGstin(stateCode, pan, rng.Next(1, 10).ToString());
            var mobile = $"{rng.Next(6, 10)}{D(9)}";
            var email = $"{surname.ToLowerInvariant()}.{D(3)}@example.com";
            var incorp = DateTime.Today.AddDays(-rng.Next(365 * 2, 365 * 12));

            var model = new UdyamResponseModel
            {
                client_id = "FRL-DUMMY-" + D(6),
                uan = uan,
                certificate_url = $"https://udyamregistration.gov.in/certificate/{uan}.pdf",
                main_details = new UdyamResponseModel.MainDetailsModel
                {
                    enterprise_type_list = new()
                    {
                        new() { classification_year = incorp.Year.ToString(), enterprise_type = "Micro", classification_date = incorp }
                    },
                    name_of_enterprise = enterprise,
                    major_activity = rng.Next(2) == 0 ? "Manufacturing" : "Services",
                    social_category = "General",
                    date_of_commencement = incorp,
                    dic_name = stateName,
                    state = stateName,
                    applied_date = incorp.AddDays(10),
                    flat = D(2), name_of_building = $"{surname} Complex", road = "MG Road",
                    village = stateName, block = "Block " + L(1), city = stateName, pin = D(6),
                    mobile_number = mobile, email = email,
                    organization_type = "Private Limited Company", gender = "Male",
                    date_of_incorporation = incorp, msme_dfo = stateName,
                    registration_date = incorp.AddDays(15),
                    gstin = gstin, Pan = pan
                },
                location_of_plant_details = new()
                {
                    new() { unit_name = enterprise, line_1 = "Plot " + D(2), building = $"{surname} Complex",
                            village = stateName, street = "MG Road", road = "MG Road",
                            city = stateName, pin = D(6), state = stateName, district = stateName }
                },
                nic_code = new()
                {
                    new() { nic_2_digit = D(2), nic_4_digit = D(4), nic_5_digit = D(5),
                            activity_type = "Manufacturing", added_on = incorp }
                }
            };

            return JsonConvert.SerializeObject(model);
        }

        /// <summary>
        /// Dummy MCA response mirroring the real API shape. Company name is taken
        /// from the caller (kept in sync with Udyam/GST); directors are derived from
        /// the company name, and charges (secured-loan liens) are randomised each
        /// build so every company shows a different, realistic charge history.
        /// </summary>
        public string GetDummyMca(string uan, string companyName, string? pan = null)
        { 
            var rng = Random.Shared;
            string D(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('0' + rng.Next(10))).ToArray());

            var stateTok = (uan.Split('-').ElementAtOrDefault(1) ?? "MH").ToUpperInvariant();
            var (stateCode, stateName) = _states.TryGetValue(stateTok, out var s) ? s : ("27", "Maharashtra");

            companyName = string.IsNullOrWhiteSpace(companyName) ? "FinRiskLens Enterprises Pvt Ltd" : companyName.Trim();
            var isPrivate = companyName.ToUpperInvariant().Contains("PVT") || companyName.ToUpperInvariant().Contains("PRIVATE");
            var incYear = DateTime.Today.Year - rng.Next(3, 20);
            var incorp = new DateTime(incYear, rng.Next(1, 13), rng.Next(1, 28));

            // CIN: <listed><5-digit industry><state token><year><PTC|PLC><6-digit reg>
            var cin = $"U{D(5)}{stateTok}{incYear}{(isPrivate ? "PTC" : "PLC")}{D(6)}";

            // Directors — first is the promoter (derived from the company's first token).
            var firstNames = new[] { "Rajesh", "Anita", "Vikram", "Priya", "Suresh", "Neha", "Arun", "Kavita" };
            var promoter = companyName.Split(' ').FirstOrDefault() ?? "Owner";
            var directors = new[]
            {
                new { din_number = D(8), director_name = $"{promoter} {firstNames[rng.Next(firstNames.Length)]}",
                      start_date = incorp.ToString("yyyy-MM-dd"), end_date = "1800-01-01", surrendered_din = (string?)null },
                new { din_number = D(8), director_name = $"{firstNames[rng.Next(firstNames.Length)]} {promoter}",
                      start_date = incorp.AddDays(rng.Next(30, 400)).ToString("yyyy-MM-dd"), end_date = "1800-01-01", surrendered_din = (string?)null },
            };

            // Charges — dynamic count, amounts and dates; a mix of open/modified liens.
            var assets = new[] { "Lien on Fixed Deposits (FD)", "Hypothecation of Stock & Book Debts",
                                 "Mortgage of Immovable Property", "Charge on Plant & Machinery", "Lien on Current Assets" };
            var charges = Enumerable.Range(0, rng.Next(3, 9)).Select(_ =>
            {
                var created = incorp.AddDays(rng.Next(60, (DateTime.Today - incorp).Days));
                var modified = rng.Next(2) == 0 ? created.AddDays(rng.Next(30, 300)) : new DateTime(1800, 1, 1);
                return new
                {
                    assets_under_charge = " " + assets[rng.Next(assets.Length)],
                    charge_amount = (rng.Next(5, 400) * 100000L).ToString(),
                    date_of_creation = created.ToString("yyyy-MM-dd"),
                    date_of_modification = modified.ToString("yyyy-MM-dd"),
                    status = "OPEN"
                };
            }).ToArray();

            var response = new
            {
                rrn = $"{D(8)}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                cin,
                status_code = "200",
                message = new
                {
                    client_id = "company_" + D(8),
                    company_id = cin,
                    company_type = "Company",
                    company_name = companyName,
                    details = new
                    {
                        company_info = new
                        {
                            cin,
                            roc_code = $"RoC-{stateName}",
                            registration_number = D(6),
                            company_category = "Company limited by Shares",
                            class_of_company = isPrivate ? "Private" : "Public",
                            company_sub_category = "Non-govt company",
                            authorized_capital = (rng.Next(1, 50) * 100000L).ToString(),
                            paid_up_capital = (rng.Next(1, 50) * 100000L).ToString(),
                            number_of_members = rng.Next(2, 50).ToString(),
                            date_of_incorporation = incorp.ToString("yyyy-MM-dd"),
                            registered_address = $"Plot {D(2)}, Industrial Area, {stateName} {stateCode}0015 IN",
                            address_other_than_ro = "-",
                            email_id = "info@example.com",
                            listed_status = "Unlisted",
                            active_compliance = (string?)null,
                            suspended_at_stock_exchange = "-",
                            last_agm_date = incorp.AddYears(1).ToString("yyyy-MM-dd"),
                            last_bs_date = incorp.AddYears(1).ToString("yyyy-MM-dd"),
                            company_status = "Active",
                            status_under_cirp = (string?)null
                        },
                        directors,
                        charges
                    }
                },
                tran_ref_no = D(8)
            };

            return JsonConvert.SerializeObject(response);
        }

        /// <summary>
        /// Dummy DIN verification response mirroring the real MCA DIN API shape (rrn /
        /// din / message / tran_ref_no). The director's name is carried through from the
        /// MCA response; father name, DOB, address and email are randomised-but-plausible.
        /// </summary>
        public string GetDummyDin(string din, string directorName, string? pan = null)
        {
            var rng = Random.Shared;
            string D(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('0' + rng.Next(10))).ToArray());
            string A(int n)
            {
                const string alnum = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
                return new(Enumerable.Range(0, n).Select(_ => alnum[rng.Next(alnum.Length)]).ToArray());
            }

            din = string.IsNullOrWhiteSpace(din) ? D(8) : din.Trim();
            var fullName = string.IsNullOrWhiteSpace(directorName) ? "Unknown Director" : directorName.Trim();
            var surname = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? fullName;

            var dob = DateTime.Today.AddYears(-rng.Next(35, 70)).AddDays(-rng.Next(0, 365));
            var buildings = new[] { "SONA MAHAL APT", "GREEN VALLEY SOC", "SHANTI NIWAS", "SUNRISE RESIDENCY", "LAKE VIEW APT", "INFOTECH TOWER" };
            var address = $"{rng.Next(1, 400)}, {buildings[rng.Next(buildings.Length)]}";
            var email = $"{surname.ToLowerInvariant()}.{D(4)}@example.com";

            var response = new
            {
                rrn = $"{D(8)}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                din,
                status_code = "200",
                message = new
                {
                    client_id = "corporate_din_" + A(20),
                    din_number = din,
                    father_name = "  " + surname.ToUpperInvariant(),
                    full_name = fullName,
                    dob = dob.ToString("yyyy-MM-dd"),
                    nationality = "IN",
                    present_address = address,
                    permanent_address = "",
                    email,
                    pan_number = string.IsNullOrWhiteSpace(pan) ? null : pan.Trim(),
                    companies_associated = Array.Empty<object>(),
                    status = "success"
                },
                tran_ref_no = D(8)
            };

            return JsonConvert.SerializeObject(response);
        }

        /// <summary>
        /// Dummy ITR response mirroring the real ITR API vendor's contract. Proprietorships
        /// get the presumptive ITR-4 (SUGAM) shape (source: itr_response_proprietorship_itr4.json);
        /// every other constitution gets the books-of-account ITR-5 shape (source:
        /// itr_response_partnership_llp_itr5.json) — the only two response shapes this API
        /// has documented so far, so partnerships, LLPs and companies all share the ITR-5 one.
        /// <para>
        /// This mirrors the <b>real vendor API</b> shape, and is what
        /// FinRiskLensAI.ML.Features.ItrFeatureExtractor parses — both branches below feed
        /// the scoring pipeline. Changing a field name here means changing the extractor
        /// with it, or the feature silently stops being read.
        /// </para>
        /// </summary>
        public string GetDummyItr(string uan, string entityName, string constitutionType,
            string? pan = null, string? gstin = null, string? email = null, string? mobile = null,
            string? addressLine1 = null, string? city = null, string? state = null, string? pincode = null,
            IReadOnlyList<AaAccountInfo>? aaAccounts = null, string? assessmentYear = null)
        {
            var rng = Random.Shared;
            string D(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('0' + rng.Next(10))).ToArray());
            string L(int n) => new(Enumerable.Range(0, n).Select(_ => (char)('A' + rng.Next(26))).ToArray());

            var stateTok = (uan?.Split('-').ElementAtOrDefault(1) ?? "MH").ToUpperInvariant();
            var (stateCode, fallbackStateName) = _states.TryGetValue(stateTok, out var s) ? s : ("27", "Maharashtra");
            var stateName = string.IsNullOrWhiteSpace(state) ? fallbackStateName : state.Trim();

            // Identity fields use the real onboarded values whenever supplied — only
            // whichever ones are missing fall back to a random-but-valid placeholder.
            entityName = string.IsNullOrWhiteSpace(entityName) ? "FinRiskLens Enterprises" : entityName.Trim();
            pan = string.IsNullOrWhiteSpace(pan) ? $"{L(5)}{D(4)}{L(1)}" : pan.Trim().ToUpperInvariant();
            gstin = string.IsNullOrWhiteSpace(gstin) ? BuildGstin(stateCode, pan, rng.Next(1, 10).ToString()) : gstin.Trim().ToUpperInvariant();
            mobile = string.IsNullOrWhiteSpace(mobile) ? $"{rng.Next(6, 10)}{D(9)}" : mobile.Trim();
            var fallbackEmail = $"{entityName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "info"}.{D(3)}@example.com";
            email = string.IsNullOrWhiteSpace(email) ? fallbackEmail : email.Trim();

            // No hardcoded year — default to the assessment year for the most recently
            // completed Indian financial year (Apr-Mar) as of today, so the return
            // always looks freshly filed regardless of when this runs.
            assessmentYear = string.IsNullOrWhiteSpace(assessmentYear) ? ComputeAssessmentYear() : assessmentYear.Trim();
            var ayStart = int.TryParse(assessmentYear.Split('-').ElementAtOrDefault(0), out var ay) ? ay : DateTime.Today.Year;
            var fyStart = ayStart - 1;
            var financialYear = $"{fyStart}-{(fyStart + 1) % 100:00}";
            var filingDate = new DateTime(ayStart, rng.Next(7, 11), rng.Next(1, 28));
            var ackNumber = D(15);

            var address = new
            {
                line1 = string.IsNullOrWhiteSpace(addressLine1)
                    ? $"{(rng.Next(2) == 0 ? "Shop No." : "Gate No.")} {rng.Next(1, 200)}, {stateName} Industrial Area"
                    : addressLine1.Trim(),
                city = string.IsNullOrWhiteSpace(city) ? stateName : city.Trim(),
                state = stateName,
                pincode = string.IsNullOrWhiteSpace(pincode) ? D(6) : pincode.Trim(),
                country = "India"
            };

            // Real linked accounts from the Account Aggregator statement, when available —
            // only falls back to one random-but-valid account when the MSME hasn't
            // completed AA consent yet, so there's still a bank_details entry to show.
            var bankDetails = aaAccounts != null && aaAccounts.Count > 0
                ? aaAccounts.Select((acc, idx) => new
                {
                    bank_name = string.IsNullOrWhiteSpace(acc.FipName) || acc.FipName == "-" ? "Bank" : acc.FipName,
                    account_number_masked = string.IsNullOrWhiteSpace(acc.MaskedAccountNumber) || acc.MaskedAccountNumber == "-" ? "XXXXXXXX" + D(4) : acc.MaskedAccountNumber,
                    ifsc_code = string.IsNullOrWhiteSpace(acc.IfscCode) || acc.IfscCode == "-" ? $"{L(4)}0{D(6)}" : acc.IfscCode,
                    account_type = string.IsNullOrWhiteSpace(acc.Type) || acc.Type == "-" ? "Current" : acc.Type,
                    is_refund_account = idx == 0
                }).ToArray()
                : new[]
                {
                    new
                    {
                        bank_name = new[] { "State Bank of India", "Bank of Maharashtra", "HDFC Bank", "ICICI Bank", "Punjab National Bank" }[rng.Next(5)],
                        account_number_masked = "XXXXXXXX" + D(4),
                        ifsc_code = $"{L(4)}0{D(6)}",
                        account_type = "Current",
                        is_refund_account = true
                    }
                };

            var isProprietorship = string.Equals(constitutionType?.Trim(), "PROPRIETORSHIP", StringComparison.OrdinalIgnoreCase)
                || (constitutionType?.Contains("Proprietor", StringComparison.OrdinalIgnoreCase) ?? false);

            object data;
            if (isProprietorship)
            {
                // ── ITR-4 (SUGAM) — presumptive taxation under section 44AD.
                var turnover = rng.Next(15, 90) * 100_000L;                 // ₹15L – 90L
                var cashShare = Math.Round(rng.NextDouble() * 0.15, 2);      // most businesses bank-heavy
                var cashTurnover = (long)(turnover * cashShare);
                var bankTurnover = turnover - cashTurnover;
                var presumptiveRate = 8.0;
                var presumptiveIncome = (long)Math.Round(turnover * presumptiveRate / 100);
                var otherIncome = rng.Next(5, 80) * 1000L;
                var grossTotalIncome = presumptiveIncome + otherIncome;
                var deduction80c = rng.Next(0, 150) * 1000L;
                var deduction80d = rng.Next(0, 25) * 1000L;
                var totalDeductions = deduction80c + deduction80d;
                var totalIncome = Math.Max(0, grossTotalIncome - totalDeductions);

                // Slab tax on totalIncome (old regime, simplified), then §87A rebate if <= 5L (old) / <= 7L (new) taxable income.
                double SlabTax(long income)
                {
                    double tax = 0;
                    if (income > 1_000_000) { tax += (income - 1_000_000) * 0.30; income = 1_000_000; }
                    if (income > 500_000) { tax += (income - 500_000) * 0.20; income = 500_000; }
                    if (income > 250_000) { tax += (income - 250_000) * 0.05; }
                    return tax;
                }
                var taxOnIncome = (long)Math.Round(SlabTax(totalIncome));
                var rebate87a = totalIncome <= 700_000 ? taxOnIncome : 0;
                var cess = (long)Math.Round((taxOnIncome - rebate87a) * 0.04);
                var totalTaxLiability = Math.Max(0, taxOnIncome - rebate87a + cess);
                var tds = rng.Next(0, 30) * 1000L;
                var totalTaxesPaid = tds;
                var refundOrDemandAmount = totalTaxesPaid - totalTaxLiability;

                data = new
                {
                    source_identifiers = new { udyam_registration_number = uan, gstin, pan },
                    entity_details = new
                    {
                        constitution_type = "PROPRIETORSHIP",
                        proprietor_name = entityName,
                        trade_name = entityName,
                        date_of_commencement = DateTime.Today.AddYears(-rng.Next(2, 12)).ToString("yyyy-MM-dd"),
                        registered_address = address,
                        email,
                        mobile
                    },
                    filing_details = new
                    {
                        itr_form_type = "ITR-4 (SUGAM)",
                        assessment_year = assessmentYear,
                        financial_year = financialYear,
                        filing_type = "ORIGINAL",
                        filing_section = "139(1)",
                        acknowledgement_number = ackNumber,
                        filing_date = filingDate.ToString("yyyy-MM-dd"),
                        filing_mode = "EVC",
                        e_verification_status = "VERIFIED",
                        e_verification_date = filingDate.ToString("yyyy-MM-dd"),
                        return_status = "PROCESSED"
                    },
                    presumptive_income_details = new
                    {
                        scheme_section = "44AD",
                        nature_of_business_code = "09028 - Retail sale of hardware, paints and glass",
                        gross_turnover_or_gross_receipts = turnover,
                        turnover_through_banking_channels = bankTurnover,
                        turnover_in_cash = cashTurnover,
                        presumptive_income_declared = presumptiveIncome,
                        effective_rate_declared_percent = presumptiveRate
                    },
                    income_details = new
                    {
                        income_from_business_presumptive = presumptiveIncome,
                        income_from_house_property = 0,
                        income_from_other_sources = otherIncome,
                        gross_total_income = grossTotalIncome
                    },
                    deductions_chapter_via = new { section_80c = deduction80c, section_80d = deduction80d, total_deductions = totalDeductions },
                    total_income = totalIncome,
                    tax_computation = new
                    {
                        tax_on_total_income = taxOnIncome,
                        rebate_us_87a = rebate87a,
                        health_and_education_cess = cess,
                        total_tax_liability = totalTaxLiability,
                        interest_us_234a = 0,
                        interest_us_234b = 0,
                        interest_us_234c = 0,
                        total_tax_and_interest = totalTaxLiability,
                        taxes_paid = new { advance_tax = 0L, tds, tcs = 0L, self_assessment_tax = 0L, total_taxes_paid = totalTaxesPaid },
                        refund_or_demand = new
                        {
                            type = refundOrDemandAmount > 0 ? "REFUND" : refundOrDemandAmount < 0 ? "DEMAND" : "NIL",
                            amount = Math.Abs(refundOrDemandAmount)
                        }
                    },
                    no_account_case_financials = new
                    {
                        note = "Presumptive filers under 44AD are not required to maintain regular books; only summary figures are reported",
                        total_sundry_debtors = (long)(turnover * 0.06),
                        total_sundry_creditors = (long)(turnover * 0.04),
                        total_stock_in_trade = (long)(turnover * 0.08),
                        cash_in_hand_and_bank = (long)(turnover * 0.025)
                    },
                    gst_turnover_reconciliation = GstReconciliation(turnover, rng),
                    bank_details = bankDetails,
                    verification = new
                    {
                        verified_by_name = entityName,
                        designation = "Proprietor",
                        verified_by_pan = pan,
                        verification_date = filingDate.ToString("yyyy-MM-dd"),
                        place = stateName
                    }
                };
            }
            else
            {
                // ── ITR-5 — partnership / LLP / company, books-of-account filer.
                var isLlp = constitutionType?.Contains("LLP", StringComparison.OrdinalIgnoreCase) ?? false;
                var revenue = rng.Next(80, 500) * 100_000L;                  // ₹80L – 5Cr
                var expenseRatio = 0.75 + rng.NextDouble() * 0.15;           // 75–90% of revenue
                var totalExpenses = (long)Math.Round(revenue * expenseRatio);
                var otherIncome = rng.Next(2, 20) * 10_000L;
                var netProfitBeforeTax = revenue + otherIncome - totalExpenses;

                var remuneration = (long)Math.Round(netProfitBeforeTax * (0.15 + rng.NextDouble() * 0.10));
                var interestOnCapital = (long)Math.Round(netProfitBeforeTax * 0.05);
                var allowable40b = remuneration + interestOnCapital;
                var pgbp = Math.Max(0, netProfitBeforeTax - allowable40b);
                var grossTotalIncome = pgbp + otherIncome;
                var deduction80g = rng.Next(0, 30) * 1000L;
                var totalIncome = Math.Max(0, grossTotalIncome - deduction80g);

                var taxRate = 30;
                var taxOnIncome = (long)Math.Round(totalIncome * taxRate / 100.0);
                var cess = (long)Math.Round(taxOnIncome * 0.04);
                var totalTaxLiability = taxOnIncome + cess;
                var advanceTax = (long)Math.Round(totalTaxLiability * (0.70 + rng.NextDouble() * 0.15));
                var tds = (long)Math.Round(totalTaxLiability * 0.15);
                var interest234b = (long)Math.Round(totalTaxLiability * 0.008);
                var interest234c = (long)Math.Round(totalTaxLiability * 0.0025);
                var totalTaxAndInterest = totalTaxLiability + interest234b + interest234c;
                var selfAssessment = Math.Max(0, totalTaxAndInterest - advanceTax - tds);
                var totalTaxesPaid = advanceTax + tds + selfAssessment;

                var partnerNames = new[] { "Deshpande", "Kulkarni", "Joshi", "Naik", "Shinde", "Rao" };
                var firstNames = new[] { "Anil", "Vinod", "Rakesh", "Sunita", "Meera", "Sanjay" };
                var p1Name = $"{firstNames[rng.Next(firstNames.Length)]} {partnerNames[rng.Next(partnerNames.Length)]}";
                var p2Name = $"{firstNames[rng.Next(firstNames.Length)]} {partnerNames[rng.Next(partnerNames.Length)]}";
                var p1Share = 50 + rng.Next(0, 20);
                var partnersDetails = new[]
                {
                    new
                    {
                        name = p1Name, pan = $"{L(5)}{D(4)}{L(1)}",
                        profit_sharing_ratio_percent = p1Share,
                        capital_balance_as_on_year_end = rng.Next(10, 30) * 100_000L,
                        is_working_partner = true
                    },
                    new
                    {
                        name = p2Name, pan = $"{L(5)}{D(4)}{L(1)}",
                        profit_sharing_ratio_percent = 100 - p1Share,
                        capital_balance_as_on_year_end = rng.Next(8, 25) * 100_000L,
                        is_working_partner = true
                    }
                };

                var totalPartnersCapital = partnersDetails.Sum(p => p.capital_balance_as_on_year_end);
                var totalLiabilities = totalPartnersCapital + (long)(revenue * 0.20);
                var totalAssets = totalLiabilities + (long)(revenue * 0.12);

                var isAuditApplicable = revenue > 10_000_000; // > ₹1 Cr, simplified 44AB trigger
                var auditDetails = isAuditApplicable
                    ? new
                    {
                        is_tax_audit_applicable = true,
                        audit_section = "44AB",
                        auditor_name = $"{L(1)}. {L(1)}. {partnerNames[rng.Next(partnerNames.Length)]} & Co.",
                        auditor_membership_number = D(6),
                        udin = $"{DateTime.Today.Year % 100}{D(6)}AAAAA{D(4)}",
                        form_3ca_3cb_filing_date = filingDate.AddMonths(-1).ToString("yyyy-MM-dd")
                    }
                    : (object)new
                    {
                        is_tax_audit_applicable = false,
                        audit_section = (string?)null,
                        auditor_name = (string?)null,
                        auditor_membership_number = (string?)null,
                        udin = (string?)null,
                        form_3ca_3cb_filing_date = (string?)null
                    };

                data = new
                {
                    source_identifiers = new { udyam_registration_number = uan, gstin, pan },
                    entity_details = new
                    {
                        constitution_type = isLlp ? "LLP" : "PARTNERSHIP_FIRM",
                        firm_name = entityName,
                        llpin = isLlp ? $"AA{L(1)}-{D(4)}" : null,
                        date_of_formation = DateTime.Today.AddYears(-rng.Next(3, 20)).ToString("yyyy-MM-dd"),
                        registered_address = address,
                        email,
                        mobile
                    },
                    filing_details = new
                    {
                        itr_form_type = "ITR-5",
                        assessment_year = assessmentYear,
                        financial_year = financialYear,
                        filing_type = "ORIGINAL",
                        filing_section = "139(1)",
                        acknowledgement_number = ackNumber,
                        filing_date = filingDate.ToString("yyyy-MM-dd"),
                        filing_mode = "Digital Signature Certificate (DSC)",
                        e_verification_status = "VERIFIED",
                        e_verification_date = filingDate.ToString("yyyy-MM-dd"),
                        return_status = "PROCESSED"
                    },
                    partners_details = partnersDetails,
                    income_details = new
                    {
                        profits_and_gains_of_business_or_profession = pgbp,
                        income_from_house_property = 0,
                        capital_gains = 0,
                        income_from_other_sources = otherIncome,
                        gross_total_income = grossTotalIncome
                    },
                    partner_remuneration_and_interest = new
                    {
                        total_remuneration_paid_to_partners = remuneration,
                        total_interest_on_capital_paid = interestOnCapital,
                        allowable_under_section_40b = allowable40b,
                        amount_disallowed = 0
                    },
                    deductions_chapter_via = new { section_80g = deduction80g, total_deductions = deduction80g },
                    total_income = totalIncome,
                    tax_computation = new
                    {
                        tax_rate_applied_percent = taxRate,
                        tax_on_total_income = taxOnIncome,
                        surcharge = 0,
                        health_and_education_cess = cess,
                        total_tax_liability = totalTaxLiability,
                        alternate_minimum_tax_us_115jc = new { applicable = false, amt_amount = 0 },
                        interest_us_234a = 0,
                        interest_us_234b = interest234b,
                        interest_us_234c = interest234c,
                        total_tax_and_interest = totalTaxAndInterest,
                        taxes_paid = new { advance_tax = advanceTax, tds, tcs = 0L, self_assessment_tax = selfAssessment, total_taxes_paid = totalTaxesPaid },
                        refund_or_demand = new { type = "NIL", amount = 0 }
                    },
                    balance_sheet_summary = new
                    {
                        as_on_date = new DateTime(ayStart, 3, 31).ToString("yyyy-MM-dd"),
                        total_partners_capital = totalPartnersCapital,
                        total_liabilities = totalLiabilities,
                        total_assets = totalAssets
                    },
                    profit_and_loss_summary = new
                    {
                        period = $"{fyStart}-04-01 to {ayStart}-03-31",
                        revenue_from_operations = revenue,
                        other_income = otherIncome,
                        total_expenses = totalExpenses,
                        net_profit_before_tax = netProfitBeforeTax,
                        net_profit_after_tax = netProfitBeforeTax - totalTaxLiability
                    },
                    gst_turnover_reconciliation = GstReconciliation(revenue, rng),
                    audit_details = auditDetails,
                    bank_details = bankDetails,
                    verification = new
                    {
                        verified_by_name = p1Name,
                        designation = isLlp ? "Designated Partner" : "Managing Partner",
                        verified_by_pan = partnersDetails[0].pan,
                        verification_date = filingDate.ToString("yyyy-MM-dd"),
                        place = stateName
                    }
                };
            }

            var response = new
            {
                request_id = $"REQ-ITR-{DateTime.Today:yyyyMMdd}-{D(6)}",
                status = "SUCCESS",
                message = "ITR details fetched successfully",
                timestamp = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                data,
                errors = Array.Empty<object>()
            };

            return JsonConvert.SerializeObject(response);
        }

        /// <summary>
        /// The Indian assessment year (AY) for the most recently completed financial
        /// year (Apr-Mar) as of today, e.g. "2026-27" if run any time from Apr 2026
        /// through Mar 2027 — never a fixed value. <paramref name="cyclesAgo"/> steps
        /// back whole AY cycles (1 = last year's AY) for callers that need history.
        /// </summary>
        private static string ComputeAssessmentYear(int cyclesAgo = 0)
        {
            var today = DateTime.Today;
            var ayStartYear = (today.Month >= 4 ? today.Year : today.Year - 1) - cyclesAgo;
            return $"{ayStartYear}-{(ayStartYear + 1) % 100:00}";
        }

        /// <summary>GST-vs-ITR declared turnover reconciliation, kept within the vendor's "tolerance" band.</summary>
        private static object GstReconciliation(long itrTurnover, Random rng)
        {
            var variancePercent = Math.Round(rng.NextDouble() * 3, 2); // 0–3%, within tolerance
            var varianceAmount = (long)Math.Round(itrTurnover * variancePercent / 100);
            return new
            {
                gst_annual_turnover_reported = itrTurnover + varianceAmount,
                itr_declared_turnover = itrTurnover,
                variance_amount = varianceAmount,
                variance_percent = variancePercent,
                reconciliation_status = "WITHIN_TOLERANCE"
            };
        }

        // Standard GSTIN check-digit: base-36, alternating weights 1,2 over the first 14 chars.
        private static string BuildGstin(string stateCode, string pan, string entityNo)
        {
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var body = $"{stateCode}{pan}{entityNo}Z";   // 14 chars; 15th is the checksum
            int sum = 0;
            for (int i = 0; i < body.Length; i++)
            {
                int v = alphabet.IndexOf(body[i]) * (i % 2 == 0 ? 1 : 2);
                sum += v / 36 + v % 36;
            }
            return body + alphabet[(36 - sum % 36) % 36];
        }
    }
}
