using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Common;

namespace FinRiskLensAI.Common
{
    /// <summary>
    /// Builds the assistant's view of ONE business's assessment, server-side, from the same
    /// <see cref="CustomerProfileBuilder"/> loaders the pages use. The UAN always comes from the
    /// caller's session (never the request), so a user can't reach another business's records.
    /// <para>
    /// Every module carries its provenance (stored response, generated sample, consented AA data),
    /// identifiers are masked, and the cross-source comparisons and identity checks are computed
    /// here, deterministically: Gemini explains them but never does the arithmetic or the scoring.
    /// Scores, bands and figures are copied from the existing rule-based analyses unchanged.
    /// </para>
    /// </summary>
    public class AssessmentContextBuilder
    {
        private readonly CustomerProfileBuilder _profile;
        private readonly ILogger<AssessmentContextBuilder> _log;

        public AssessmentContextBuilder(CustomerProfileBuilder profile, ILogger<AssessmentContextBuilder> log)
        {
            _profile = profile;
            _log = log;
        }

        public sealed class Result
        {
            public JsonObject Context { get; init; } = new();
            public List<string> Available { get; } = new();
            public List<string> Missing { get; } = new();
            public List<string> Suggestions { get; } = new();
        }

        public async Task<Result> BuildAsync(UserSession session, CancellationToken ct)
        {
            var uan = session.Uan;
            var result = new Result();

            // One module at a time (the loaders share scoped services, including the database
            // context); each independently, so one failing source doesn't hide the rest.
            // GST needs no load: its page shows the fixed GstSampleData.
            var udyam = await Safe("Udyam", () => _profile.GetUdyamAsync(uan));
            var aa = await Safe("AA", () => _profile.GetAaAnalysisAsync(uan, ct));
            var itr = await Safe("ITR", () => _profile.GetItrAsync(uan, ct));
            var mca = await Safe("MCA", () => _profile.GetMcaAsync(uan, ct));

            var modules = new JsonObject();
            var ctx = new JsonObject
            {
                ["business"] = new JsonObject
                {
                    ["nameOnSignIn"] = session.Name,
                    ["udyamRegistrationNumber"] = Universal.MaskUdyam(uan),
                    ["gstin"] = Mask(session.Gstin, Universal.MaskGstin),
                    ["pan"] = Mask(session.Pan, Universal.MaskPan),
                    ["identifiersNote"] = "Identifiers are partly masked."
                },
                ["modules"] = modules,
                ["generatedAt"] = DateTime.Now.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture)
            };

            // ── Business Registration (Udyam) ──
            if (udyam is { HasData: true })
            {
                result.Available.Add("Business Registration (Udyam)");
                modules["udyam"] = new JsonObject
                {
                    ["label"] = "Business Registration (Udyam)",
                    ["provenance"] = "Stored Udyam registry response retrieved at onboarding. Not a live registry lookup.",
                    ["registrationProfileScore"] = udyam.BusinessIdentityScore,
                    ["scoreNote"] = "Rule-based registration-profile score from the Udyam record only (0–100). Not a credit score.",
                    ["enterpriseName"] = udyam.EnterpriseName,
                    ["organizationType"] = udyam.OrganizationType,
                    ["enterpriseClassification"] = udyam.LatestEnterpriseType,
                    ["primaryBusinessActivity"] = udyam.MajorActivity,
                    ["registrationDate"] = Date(udyam.RegistrationDate),
                    ["commencementDate"] = Date(udyam.DateOfCommencement),
                    ["businessAgeYears"] = udyam.DateOfCommencement == default ? null : udyam.BusinessAgeYears,
                    ["classificationHistory"] = Arr(udyam.EnterpriseTypeHistory.Select(e => (JsonNode)$"{e.Year}: {e.Type}")),
                    ["location"] = $"{udyam.City}, {udyam.District}, {udyam.State}",
                    ["registeredBusinessLocations"] = udyam.PlantLocations.Count,
                    ["nicActivities"] = Arr(udyam.NicActivities.Select(n => (JsonNode)$"{n.Code} {n.Activity} ({n.ActivityType})")),
                    ["gstinOnRecord"] = udyam.GstinMasked,
                    ["panOnRecord"] = udyam.PanMasked,
                    ["verificationNote"] = "GSTIN and PAN are listed on the Udyam record; they were not independently verified."
                };
            }
            else Missing(result, "Business Registration (Udyam)", udyam?.LoadError);

            // ── Tax & Sales Compliance (GST): the page's sample dataset ──
            result.Available.Add("Tax & Sales Compliance (GST)");
            modules["gst"] = new JsonObject
            {
                ["label"] = "Tax & Sales Compliance (GST)",
                ["provenance"] = "SAMPLE DATA: the fixed illustrative dataset shown on the GST page. No live GST API is connected, and the figures are the same for every business.",
                ["period"] = "Jan–Jun of a sample year (6 months)",
                ["monthlySalesLakh_GSTR3B"] = Series(GstSampleData.Months, GstSampleData.Sales),
                ["monthlyPurchasesLakh_GSTR2B"] = Series(GstSampleData.Months, GstSampleData.Purchases),
                ["monthlyTaxLiabilityLakh_GSTR3B"] = Series(GstSampleData.Months, GstSampleData.Tax),
                ["salesTotalJanJunLakh"] = GstSampleData.SalesTotal,
                ["annualisedTurnoverRupees"] = Math.Round(GstSampleData.Annualised * 1e5),
                ["annualisedTurnover"] = Money(GstSampleData.Annualised * 1e5) + " (Jan–Jun average × 12)",
                ["salesGrowthVsSameMonthsLastYear"] = Pct(GstSampleData.SalesGrowth),
                ["purchaseGrowthVsSameMonthsLastYear"] = Pct(GstSampleData.PurchaseGrowth),
                ["returnsFiled"] = $"{GstSampleData.ReturnsFiled} of {GstSampleData.ReturnsDue} (GSTR-1 + GSTR-3B), {GstSampleData.LateReturns} late",
                ["gstr1VsGstr3bMatch"] = Pct(GstSampleData.R1Vs3bMatch),
                ["itcAvailableLakh_GSTR2B"] = GstSampleData.ItcAvailable,
                ["itcClaimedLakh_GSTR3B"] = GstSampleData.TaxItc,
                ["itcUtilization"] = Pct(GstSampleData.ItcUtil),
                ["taxPaidLakh"] = new JsonObject { ["total"] = GstSampleData.TaxTotal, ["directlyInCash"] = GstSampleData.TaxCash, ["offsetWithItc"] = GstSampleData.TaxItc },
                ["activeB2bCustomers"] = GstSampleData.B2bCustomers,
                ["activeSuppliers"] = GstSampleData.ActiveSuppliers,
                ["topSupplierShares"] = Arr(GstSampleData.Suppliers.Select(s => (JsonNode)$"{s.Name}: {Pct(s.Share)}")),
                ["gstScoreShown"] = GstSampleData.Score,
                ["gstScoreNote"] = "The 94 shown on the GST page is part of the sample; no formula calculates it.",
                ["outlookNote"] = "The page's 12-month outlook is an illustrative +16% June-to-June trend line, not a forecast model."
            };

            // ── Consent-Based Banking Data (AA) ──
            if (aa is { HasData: true })
            {
                result.Available.Add("Consent-Based Banking Data (AA)");
                var months = Math.Max(1, aa.MonthsCovered);
                modules["banking"] = new JsonObject
                {
                    ["label"] = "Consent-Based Banking Data (Account Aggregator)",
                    ["provenance"] = "Bank data shared through the Account Aggregator consent flow (Finvu test environment), analysed by the rule-based statement analysis.",
                    ["period"] = aa.AnalysisPeriod,
                    ["monthsCovered"] = aa.MonthsCovered,
                    ["accounts"] = aa.Accounts.Count,
                    ["totalCreditsRupees"] = Round(aa.TotalCredits),
                    ["totalDebitsRupees"] = Round(aa.TotalDebits),
                    ["avgMonthlyCreditRupees"] = Round(aa.AvgMonthlyCredit),
                    ["avgBalanceRupees"] = Round(aa.AvgBalance),
                    ["potentialBusinessInflowRupees"] = Round(aa.PotentialBusinessInflow),
                    ["selfTransferRupees"] = Round(aa.SelfTransferAmount),
                    ["cashDepositRatio"] = Round(aa.CashDepositRatio, 3),
                    ["cashWithdrawalRatio"] = Round(aa.CashWithdrawalRatio, 3),
                    ["passThroughRiskLevel"] = aa.PassThroughRiskLevel,
                    ["estimatedMonthlyObligationRupees"] = Round(aa.EstimatedMonthlyObligation),
                    ["negativeBalanceEvents"] = aa.NegativeBalanceEvents,
                    ["bankingHealthScore"] = aa.HealthScore,
                    ["bankingRiskGrade"] = aa.RiskGrade,
                    ["scoreComponents"] = Arr(aa.ScoreComponents.Select(c => (JsonNode)$"{c.Name}: {c.Score}/100 (weight {c.Weight}%) — {c.Reason}")),
                    ["riskFlags"] = Arr(aa.RiskFlags.Take(8).Select(f => (JsonNode)$"[{f.Severity}] {f.Title}: {f.Evidence}")),
                    ["annualisedCreditsRupees"] = Round(aa.TotalCredits / months * 12),
                    ["annualisedBusinessInflowRupees"] = Round(aa.PotentialBusinessInflow / months * 12)
                };
            }
            else Missing(result, "Consent-Based Banking Data (AA)", aa?.LoadError ?? "Bank statements have not been shared through the Account Aggregator yet.");

            // ── Income Tax Filing (ITR) ──
            long? itrTurnover = null;
            if (itr is { HasData: true })
            {
                result.Available.Add("Income Tax Filing (ITR)");
                itrTurnover = itr.HasFinancials ? itr.RevenueFromOperations : itr.HasPresumptive ? itr.PresumptiveTurnover : null;
                modules["itr"] = new JsonObject
                {
                    ["label"] = "Income Tax Filing (ITR)",
                    ["provenance"] = "SAMPLE DATA: a generated return. Not filed with or fetched from the Income Tax Department.",
                    ["form"] = itr.FormType,
                    ["assessmentYear"] = itr.AssessmentYear,
                    ["financialYear"] = itr.FinancialYear,
                    ["entityNameOnReturn"] = itr.EntityName,
                    ["constitutionOnReturn"] = itr.ConstitutionType,
                    ["filedOn"] = itr.FilingDateText,
                    ["dueDate"] = itr.DueDateText,
                    ["filedOnTime"] = itr.FiledOnTime,
                    ["returnStatus"] = itr.ReturnStatus,
                    ["businessIncomeRupees"] = itr.BusinessIncome,
                    ["otherSourcesIncomeRupees"] = itr.OtherSourcesIncome,
                    ["grossTotalIncomeRupees"] = itr.GrossTotalIncome,
                    ["deductionsRupees"] = itr.TotalDeductions,
                    ["totalIncomeRupees"] = itr.TotalIncome,
                    ["totalTaxAndInterestRupees"] = itr.TotalTaxAndInterest,
                    ["taxesPaidRupees"] = itr.TotalTaxesPaid,
                    ["interest234B234CRupees"] = itr.Interest234B + itr.Interest234C,
                    ["refundOrDemand"] = $"{itr.RefundOrDemandType} {itr.RefundOrDemandAmount}",
                    ["turnoverRupees"] = itrTurnover,
                    ["profitBeforeTaxRupees"] = itr.HasFinancials ? itr.NetProfitBeforeTax : null,
                    ["profitAfterTaxRupees"] = itr.HasFinancials ? itr.NetProfitAfterTax : null,
                    ["netMarginPct"] = itr.NetMarginPct,
                    ["capitalRupees"] = itr.HasBalanceSheet ? itr.TotalPartnersCapital : null,
                    ["totalLiabilitiesRupees"] = itr.HasBalanceSheet ? itr.TotalLiabilities : null,
                    ["totalAssetsRupees"] = itr.HasBalanceSheet ? itr.TotalAssets : null,
                    ["debtToEquity"] = itr.DebtToEquity,
                    ["partnersOnReturn"] = Arr(itr.Partners.Select(p => (JsonNode)$"{p.Name} ({p.SharePercent:0.##}%)")),
                    ["taxAudit"] = itr.AuditApplicable ? (itr.AuditCompleted ? "Audit report on the return" : "Audit applicable, report not filed") : "Not applicable",
                    ["gstTurnoverOnReturnRupees"] = itr.HasGstReconciliation ? itr.GstReportedTurnover : null,
                    ["filingHealthScore"] = itr.Score,
                    ["filingHealthRisk"] = itr.RiskLabel,
                    ["scoreComponents"] = Arr(itr.ScoreComponents.Select(c => (JsonNode)$"{c.Name}: {c.Points}/{c.MaxPoints} — {c.Reason}")),
                    ["ruleBasedObservations"] = Arr(itr.Observations.Select(o => (JsonNode)o.Text))
                };
            }
            else Missing(result, "Income Tax Filing (ITR)", itr?.LoadError ?? "No income-tax return has been fetched yet.");

            // ── Corporate Registry (MCA) ──
            long openCharges = 0;
            if (mca is { HasData: true, IsApplicable: true })
            {
                result.Available.Add("Corporate Registry (MCA)");
                openCharges = mca.Charges.Where(c => c.IsOpen).Sum(c => c.Amount);
                modules["mca"] = new JsonObject
                {
                    ["label"] = "Corporate Registry (MCA)",
                    ["provenance"] = "SAMPLE DATA: a generated MCA record. No live MCA access; DIN, OFAC and AML checks are not live.",
                    ["companyName"] = mca.CompanyName,
                    ["cin"] = mca.Cin,
                    ["status"] = mca.Status,
                    ["companyClass"] = mca.CompanyClass,
                    ["category"] = mca.Category,
                    ["incorporated"] = mca.IncorporationDateText,
                    ["authorizedCapital"] = mca.AuthorizedCapitalText,
                    ["paidUpCapital"] = mca.PaidUpCapitalText,
                    ["lastAgm"] = mca.LastAgmDate,
                    ["lastBalanceSheet"] = mca.LastBsDate,
                    ["cirpStatusOnRecord"] = mca.CirpText,
                    ["directors"] = Arr(mca.Directors.Select(d => (JsonNode)$"{d.Name}, appointed {d.AppointedText}, {(d.IsActive ? "active" : "ceased")}")),
                    ["registeredCharges"] = Arr(mca.Charges.Select(c => (JsonNode)$"{c.AmountShort} {c.Status}, created {c.CreatedText}, on {c.Asset}")),
                    ["openChargeCount"] = mca.OpenChargeCount,
                    ["openChargesTotalRupees"] = openCharges,
                    ["chargesNote"] = "A registered charge is security recorded over company assets. It is not an outstanding loan balance.",
                    ["registryScore"] = mca.Score,
                    ["registryRiskBand"] = mca.RiskLabel,
                    ["scoreBreakdownPct"] = $"Legal {mca.LegalPct}, Governance {mca.GovernancePct}, Charges {mca.ChargesPct}, Compliance {mca.CompliancePct}"
                };
            }
            else if (mca is { IsApplicable: false } && udyam is { HasData: true })
                result.Missing.Add("Corporate Registry (MCA) — not applicable: only companies and LLPs are on the MCA register");
            else if (mca is { IsApplicable: false })
                result.Missing.Add("Corporate Registry (MCA) — unknown whether it applies: the Udyam record that gives the constitution is missing");
            else Missing(result, "Corporate Registry (MCA)", mca?.LoadError ?? "No MCA record has been fetched yet.");

            // ── Cross-source comparisons, computed here ──
            var gstAnnual = GstSampleData.Annualised * 1e5;
            var comparisons = new JsonArray();
            void Compare(string id, string label, string formula, string left, double? l, string right, double? r, string periods)
            {
                if (l is not > 0 || r is not > 0) return;
                comparisons.Add(new JsonObject
                {
                    ["id"] = id, ["comparison"] = label, ["formula"] = formula,
                    [left] = Money(l.Value), [right] = Money(r.Value),
                    ["ratio"] = Math.Round(l.Value / r.Value, 2),
                    ["differencePct"] = Math.Round((l.Value - r.Value) / r.Value * 100, 1),
                    ["periods"] = periods
                });
            }

            Compare("gst_vs_itr_turnover", "GST annualised turnover vs ITR turnover", "GST ÷ ITR",
                "gst", gstAnnual, "itr", itrTurnover, $"GST: Jan–Jun sample year annualised; ITR: FY {itr?.FinancialYear}");
            if (aa is { HasData: true })
            {
                var m = Math.Max(1, aa.MonthsCovered);
                Compare("gst_vs_bank_credits", "GST annualised turnover vs annualised bank credits", "GST ÷ bank credits",
                    "gst", gstAnnual, "bankCredits", (double)(aa.TotalCredits / m * 12), $"GST: Jan–Jun sample year; bank: {aa.AnalysisPeriod}");
                Compare("itr_vs_bank_business_inflow", "ITR turnover vs annualised bank business inflow", "ITR ÷ bank business inflow",
                    "itr", itrTurnover, "bankBusinessInflow", (double)(aa.PotentialBusinessInflow / m * 12), $"ITR: FY {itr?.FinancialYear}; bank: {aa.AnalysisPeriod}");
            }
            Compare("mca_open_charges_vs_itr_turnover", "MCA open registered charges vs ITR turnover", "open charges ÷ ITR turnover",
                "openCharges", openCharges, "itr", itrTurnover, "Charges: as on the MCA record; ITR: FY " + itr?.FinancialYear);
            Compare("mca_open_charges_vs_gst_turnover", "MCA open registered charges vs GST annualised turnover", "open charges ÷ GST turnover",
                "openCharges", openCharges, "gst", gstAnnual, "Charges: as on the MCA record; GST: Jan–Jun sample year annualised");
            if (itr is { HasGstReconciliation: true })
                Compare("itr_gst_figure_vs_gst_page", "GST turnover quoted on the ITR vs the GST page's annualised turnover", "ITR's GST figure ÷ GST page",
                    "itrGstFigure", itr.GstReportedTurnover, "gstPage", gstAnnual, "Both are sample data from different generators");
            ctx["crossSourceComparisons"] = comparisons;

            // ── Identity consistency: do the records describe the same business? ──
            var checks = new JsonArray();
            void Check(string field, IEnumerable<(string Source, string? Value)> values, Func<string, string> norm)
            {
                var present = values.Where(v => !string.IsNullOrWhiteSpace(v.Value) && v.Value != "-").ToList();
                if (present.Count < 2) return;
                var distinct = present.Select(v => norm(v.Value!)).Distinct().Count();
                checks.Add(new JsonObject
                {
                    ["field"] = field,
                    ["values"] = Arr(present.Select(v => (JsonNode)$"{v.Source}: {v.Value}")),
                    ["result"] = distinct == 1 ? "match" : "mismatch"
                });
            }
            Check("Business name", new[] { ("Sign-in", session.Name), ("Udyam", udyam?.EnterpriseName), ("ITR", itr?.EntityName), ("MCA", mca?.CompanyName) }, NormName);
            Check("PAN (masked)", new[] { ("Sign-in", Mask(session.Pan, Universal.MaskPan)), ("Udyam", udyam?.PanMasked), ("ITR", itr?.PanMasked) }, s => s);
            Check("GSTIN (masked)", new[] { ("Sign-in", Mask(session.Gstin, Universal.MaskGstin)), ("Udyam", udyam?.GstinMasked), ("ITR", itr?.GstinMasked) }, s => s);
            Check("Constitution", new[] { ("Udyam", udyam?.OrganizationType), ("ITR", itr?.ConstitutionType), ("MCA", mca?.HasData == true ? mca.CompanyClass + " company" : null) }, NormConstitution);
            if (itr is { Partners.Count: > 0 } && mca is { HasData: true, Directors.Count: > 0 })
            {
                var partners = itr.Partners.Select(p => NormName(p.Name)).ToHashSet();
                var overlap = mca.Directors.Count(d => partners.Contains(NormName(d.Name)));
                checks.Add(new JsonObject
                {
                    ["field"] = "People (ITR partners vs MCA directors)",
                    ["values"] = Arr(new JsonNode[] {
                        "ITR: " + string.Join(", ", itr.Partners.Select(p => p.Name)),
                        "MCA: " + string.Join(", ", mca.Directors.Select(d => d.Name)) }),
                    ["result"] = overlap > 0 ? "partial or full match" : "mismatch"
                });
            }
            ctx["identityConsistency"] = checks;

            // ── Missing and stale evidence ──
            var gaps = new JsonArray(result.Missing.Select(m => (JsonNode)m).ToArray());
            if (mca is { HasData: true } && DateTime.TryParse(mca.LastAgmDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var agm)
                && agm < DateTime.Today.AddMonths(-18))
                gaps.Add($"MCA: last AGM on record is {mca.LastAgmDate}, more than 18 months ago (filings may be outdated).");
            gaps.Add("Customer-uploaded documents and the bank's fraud findings are not part of this assistant's context.");
            ctx["missingOrStale"] = gaps;

            BuildSuggestions(result, checks, comparisons);
            result.Context["assessment"] = ctx;
            return result;

            async Task<T?> Safe<T>(string name, Func<Task<T>> load) where T : class
            {
                try { return await load(); }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Assistant context: {Module} failed to load for {Uan}", name, uan);
                    return null;
                }
            }
        }

        private static void BuildSuggestions(Result r, JsonArray checks, JsonArray comparisons)
        {
            var s = r.Suggestions;
            s.Add("Summarize this business's financial health.");
            if (comparisons.Any(c => c?["id"]?.GetValue<string>() == "gst_vs_itr_turnover"))
                s.Add("Compare GST turnover with ITR turnover.");
            if (comparisons.Any(c => c?["id"]?.GetValue<string>() == "gst_vs_bank_credits"))
                s.Add("Compare GST sales with bank credits.");
            if (comparisons.Any(c => c?["id"]?.GetValue<string>()?.StartsWith("mca_open_charges") == true))
                s.Add("Do the MCA registered charges look large against turnover?");
            if (checks.Any(c => c?["result"]?.GetValue<string>() == "mismatch"))
                s.Add("Which records don't match across sources?");
            if (r.Missing.Count > 0)
                s.Add("What information is missing from this assessment?");
            s.Add("What are the top three credit risks?");
            s.Add("What should a banker verify next?");
        }

        // ── helpers ──

        public sealed record UserSession(string Uan, string? Name, string? Pan, string? Gstin);

        private static void Missing(Result r, string module, string? reason) =>
            r.Missing.Add(string.IsNullOrWhiteSpace(reason) ? module : $"{module} — {reason}");

        private static string? Mask(string? value, Func<string?, string> masker) =>
            string.IsNullOrWhiteSpace(value) ? null : masker(value);

        private static JsonArray Arr(IEnumerable<JsonNode> items) => new(items.ToArray());

        private static JsonObject Series(string[] months, double[] values)
        {
            var o = new JsonObject();
            for (var i = 0; i < months.Length; i++) o[months[i]] = values[i];
            return o;
        }

        private static string? Date(DateTime d) => d == default ? null : d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
        private static string Pct(double v) => (v * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        private static long Round(decimal v) => (long)Math.Round(v);
        private static double Round(decimal v, int digits) => (double)Math.Round(v, digits);

        /// <summary>Indian short form, same scale as the pages: ₹1.15 Cr / ₹17.86 L.</summary>
        private static string Money(double v) =>
            v >= 1e7 ? $"₹{(v / 1e7).ToString("0.##", CultureInfo.InvariantCulture)} Cr"
            : v >= 1e5 ? $"₹{(v / 1e5).ToString("0.##", CultureInfo.InvariantCulture)} L"
            : $"₹{v.ToString("N0", CultureInfo.InvariantCulture)}";

        private static string NormName(string s) =>
            Regex.Replace(s.ToUpperInvariant().Replace("PRIVATE", "PVT").Replace("LIMITED", "LTD").Replace("M/S", ""), "[^A-Z0-9]", "");

        private static string NormConstitution(string s)
        {
            var u = s.ToUpperInvariant();
            if (u.Contains("PRIVATE") || u.Contains("PVT")) return "PRIVATE COMPANY";
            if (u.Contains("PUBLIC")) return "PUBLIC COMPANY";
            if (u.Contains("LLP") || u.Contains("LIMITED LIABILITY")) return "LLP";
            if (u.Contains("PARTNER")) return "PARTNERSHIP";
            if (u.Contains("PROPRIETOR")) return "PROPRIETORSHIP";
            return u.Trim();
        }
    }
}
