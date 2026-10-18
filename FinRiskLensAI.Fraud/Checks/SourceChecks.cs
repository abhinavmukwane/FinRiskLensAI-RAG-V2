using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Facts;

namespace FinRiskLensAI.Fraud.Checks
{
    /// <summary>Extracted identity attributes across Udyam, GST, ITR, and MCA for cross-source reconciliation.</summary>
    public sealed class IdentityFacts
    {
        public string? UdyamPan { get; set; }
        public string? UdyamGstin { get; set; }
        public string? UdyamName { get; set; }
        public string? UdyamOrgType { get; set; }

        public string? GstPan { get; set; }
        public string? GstGstin { get; set; }
        public string? GstName { get; set; }

        public string? ItrPan { get; set; }
        public string? ItrGstin { get; set; }
        public string? ItrName { get; set; }

        public string? McaStatus { get; set; }
        public string? McaName { get; set; }
    }

    /// <summary>
    /// Checks on what the customer shared from its sources — the bank's Account Aggregator record, GST,
    /// ITR, the registries — rather than on an uploaded document (Doc/12_CUSTOMER_RISK_LENS.md §4).
    /// Pure functions, one finding per pattern per customer.
    /// </summary>
    public static class SourceChecks
    {
        private static readonly CultureInfo InIndia = new("en-IN");
        private static string Rupees(double v) => "₹" + ((decimal)Math.Round(v)).ToString("N0", InIndia);

        /// <summary>The flag titles the Account Aggregator analysis raises, and the finding each becomes.</summary>
        private static readonly (string TitleStart, string Type, FindingCategory Category)[] BankFlagTypes =
        {
            ("High potential pass-through", "AA_PASS_THROUGH", FindingCategory.Behavioural),
            ("Potential pass-through", "AA_PASS_THROUGH", FindingCategory.Behavioural),
            ("Very high cash withdrawal", "AA_CASH_DEPENDENCY", FindingCategory.Behavioural),
            ("Elevated cash withdrawal", "AA_CASH_DEPENDENCY", FindingCategory.Behavioural),
            ("Negative balance", "AA_NEGATIVE_BALANCE", FindingCategory.Behavioural),
            ("Banking discipline", "AA_DISCIPLINE_EVENTS", FindingCategory.Behavioural),
            ("High existing obligation", "AA_OBLIGATION_BURDEN", FindingCategory.HiddenLiabilities),
            ("Significant unclassified inflows", "AA_UNCLASSIFIED_INFLOWS", FindingCategory.IncomeInflation),
            ("High credit concentration", "AA_CREDIT_CONCENTRATION", FindingCategory.Behavioural),
        };

        /// <summary>
        /// The bank's own record, read the way an analyst would: every High or Medium flag of the Account
        /// Aggregator analysis becomes a finding (positive indicators are not findings). A flag this table
        /// does not know still becomes one, typed from its title, so a new analysis rule is never dropped.
        /// </summary>
        public static List<Finding> BankFlags(AaAnalysisResult? bank, int loanApplicationId)
        {
            if (bank == null || !bank.HasData) return new();
            return bank.RiskFlags
                .Where(f => f.Severity is "High" or "Medium")
                .Select(f =>
                {
                    var known = BankFlagTypes.FirstOrDefault(t => f.Title.StartsWith(t.TitleStart, StringComparison.OrdinalIgnoreCase));
                    var type = known.Type ?? "AA_" + string.Concat(f.Title.ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_'));
                    return (Type: type, Flag: f, Category: known.Type == null ? FindingCategory.Behavioural : known.Category);
                })
                .GroupBy(x => x.Type)   // one finding per pattern, at its most severe
                .Select(g => g.OrderBy(x => x.Flag.Severity == "High" ? 0 : 1).First())
                .Select(x => new Finding
                {
                    LoanApplicationID = loanApplicationId,
                    Type = x.Type,
                    Category = x.Category,
                    Severity = x.Flag.Severity == "High" ? FindingSeverity.High : FindingSeverity.Medium,
                    Title = $"{x.Flag.Title} in the bank's own record",
                    Explanation = $"{x.Flag.Evidence} {x.Flag.CreditRelevance} Read from the transactions the bank shared through " +
                                  $"Account Aggregator ({bank.AnalysisPeriod}).",
                    RecommendedAction = "Review the transactions behind this pattern on the AA tab before relying on the bank inflows.",
                    DetectedBy = FindingSource.Deterministic,
                    DedupKey = x.Type,
                    Evidence = new()
                    {
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = Clip($"Account Aggregator: {x.Flag.Evidence}", 400) }
                    }
                })
                .ToList();
        }

        /// <summary>
        /// Task T3 (Doc/13_TEAM_WORK_PLAN.md): Turnover cross-check comparing bank credits, GST turnover,
        /// and ITR business turnover (revenue from operations).
        /// </summary>
        public static Finding? Turnover(MsmeFeatureSet f, FraudOptions o, int loanApplicationId)
        {
            if (f == null) return null;

            double? gst = (f.HasGst || f.GstMonthlyAvgTurnover > 0) && f.GstMonthlyAvgTurnover > 0 ? f.GstMonthlyAvgTurnover : null;
            double? itr = (f.HasItr || f.ItrBusinessTurnover > 0) && f.ItrBusinessTurnover > 0 ? f.ItrBusinessTurnover / 12.0 : null;
            double? bank = (f.HasAa || f.AaMonthlyAvgBankCredit > 0) && f.AaMonthlyAvgBankCredit > 0 ? f.AaMonthlyAvgBankCredit : null;

            int legs = (gst.HasValue ? 1 : 0) + (itr.HasValue ? 1 : 0) + (bank.HasValue ? 1 : 0);
            if (legs < 2) return null;   // Never flag on one source alone; skip missing legs

            // 1. Bank credits above o.BankToTurnoverMax * max(GST, ITR) -> TURNOVER_MISMATCH, High
            if (bank.HasValue)
            {
                double maxTax = Math.Max(gst ?? 0, itr ?? 0);
                if (maxTax > 0 && (decimal)bank.Value > o.BankToTurnoverMax * (decimal)maxTax)
                {
                    double ratio = bank.Value / maxTax;
                    string ratioText = ratio switch
                    {
                        >= 1.95 and < 2.05 => "two times",
                        >= 2.95 and < 3.05 => "three times",
                        >= 3.95 and < 4.05 => "four times",
                        >= 4.95 and < 5.05 => "five times",
                        >= 2.0 => $"{Math.Round(ratio):0} times",
                        _ => $"{ratio:F1} times"
                    };

                    string explanation;
                    if (gst.HasValue && itr.HasValue)
                    {
                        explanation = $"Bank credits average {Rupees(bank.Value)} a month against GST turnover of {Rupees(gst.Value)} and ITR turnover of {Rupees(itr.Value)} — {ratioText} what the tax filings report.";
                    }
                    else if (gst.HasValue)
                    {
                        explanation = $"Bank credits average {Rupees(bank.Value)} a month against GST turnover of {Rupees(gst.Value)} — {ratioText} what the GST return reports.";
                    }
                    else
                    {
                        explanation = $"Bank credits average {Rupees(bank.Value)} a month against ITR turnover of {Rupees(itr!.Value)} — {ratioText} what the ITR reports.";
                    }

                    var evidence = new List<Evidence>
                    {
                        new() { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"Bank average monthly credits: {Rupees(bank.Value)}" }
                    };
                    if (gst.HasValue) evidence.Add(new() { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"GST average monthly turnover: {Rupees(gst.Value)}" });
                    if (itr.HasValue) evidence.Add(new() { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"ITR monthly business turnover: {Rupees(itr.Value)}" });

                    return new Finding
                    {
                        LoanApplicationID = loanApplicationId,
                        Type = "TURNOVER_MISMATCH",
                        Category = FindingCategory.IncomeInflation,
                        Severity = FindingSeverity.High,
                        Title = "Turnover mismatch: bank credits exceed tax filings",
                        Explanation = explanation,
                        RecommendedAction = "Verify whether non-revenue inflows (transfers, loans, capital infusion) inflate the bank statement, or if sales are under-reported to tax authorities.",
                        DetectedBy = FindingSource.Deterministic,
                        DedupKey = "TURNOVER_MISMATCH",
                        Evidence = evidence
                    };
                }
            }

            // 2. GST and ITR turnover more than o.GstItrVariancePct apart -> TURNOVER_MISMATCH, Medium
            if (gst.HasValue && itr.HasValue)
            {
                double baseTurnover = Math.Max(gst.Value, itr.Value);
                if (baseTurnover > 0)
                {
                    decimal variancePct = (decimal)(Math.Abs(gst.Value - itr.Value) / baseTurnover * 100.0);
                    if (variancePct > o.GstItrVariancePct)
                    {
                        var evidence = new List<Evidence>
                        {
                            new() { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"GST average monthly turnover: {Rupees(gst.Value)}" },
                            new() { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"ITR monthly business turnover: {Rupees(itr.Value)}" }
                        };

                        return new Finding
                        {
                            LoanApplicationID = loanApplicationId,
                            Type = "TURNOVER_MISMATCH",
                            Category = FindingCategory.IncomeInflation,
                            Severity = FindingSeverity.Medium,
                            Title = "Turnover mismatch: GST and ITR disagree",
                            Explanation = $"GST turnover of {Rupees(gst.Value)} a month and ITR business turnover of {Rupees(itr.Value)} a month differ by {variancePct:F1}% (policy threshold {o.GstItrVariancePct:0}%).",
                            RecommendedAction = "Reconcile GSTR-1 outward taxable supplies against Profit & Loss revenue from operations before relying on declared turnover.",
                            DetectedBy = FindingSource.Deterministic,
                            DedupKey = "TURNOVER_MISMATCH",
                            Evidence = evidence
                        };
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Task T4 (Doc/13_TEAM_WORK_PLAN.md): Normalises business name strings for fuzzy-equivalence comparison.
        /// Handles casing, punctuation, common prefixes ("M/s"), and corporate suffixes (PVT LTD / PRIVATE LIMITED, LLP, LTD).
        /// </summary>
        public static string NormalizeBusinessName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            var s = name.Trim().ToLowerInvariant();

            // Strip common salutations at start
            if (s.StartsWith("m/s.") || s.StartsWith("m/s "))
                s = s[4..].Trim();

            // Replace punctuation with space
            var chars = s.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray();
            s = new string(chars);

            // Collapse multiple spaces
            s = Regex.Replace(s, @"\s+", " ").Trim();

            // Standardize corporate suffixes (word boundary)
            s = Regex.Replace(s, @"\bprivate\s+limited\b", "pvt ltd");
            s = Regex.Replace(s, @"\bpvt\s+ltd\b", "pvt ltd");
            s = Regex.Replace(s, @"\blimited\s+liability\s+partnership\b", "llp");
            s = Regex.Replace(s, @"\blimited\b", "ltd");

            return s.Trim();
        }

        public static bool BusinessNamesMatch(string? name1, string? name2)
        {
            var n1 = NormalizeBusinessName(name1);
            var n2 = NormalizeBusinessName(name2);
            if (string.IsNullOrEmpty(n1) || string.IsNullOrEmpty(n2)) return true;
            return string.Equals(n1, n2, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Task T4: Parses identity fields from raw JSON payloads.
        /// </summary>
        public static IdentityFacts ParseIdentityFacts(string? udyamJson, string? gstTaxpayerJson, string? itrJson, string? mcaJson)
        {
            var facts = new IdentityFacts();

            if (!string.IsNullOrWhiteSpace(udyamJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(udyamJson);
                    var root = doc.RootElement;
                    var main = root.TryGetProperty("main_details", out var m) ? m : root;
                    if (main.ValueKind == JsonValueKind.Object)
                    {
                        facts.UdyamPan = Text(main, "Pan") ?? Text(main, "pan");
                        facts.UdyamGstin = Text(main, "gstin");
                        facts.UdyamName = Text(main, "name_of_enterprise");
                        facts.UdyamOrgType = Text(main, "organization_type");
                    }
                }
                catch (JsonException) { }
            }

            if (!string.IsNullOrWhiteSpace(gstTaxpayerJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(gstTaxpayerJson);
                    var data = At(doc.RootElement, "response", "message", "data");
                    if (data.ValueKind != JsonValueKind.Object) data = doc.RootElement;
                    if (data.ValueKind == JsonValueKind.Object)
                    {
                        facts.GstGstin = Text(data, "gstin");
                        if (!string.IsNullOrWhiteSpace(facts.GstGstin) && facts.GstGstin.Length >= 12)
                        {
                            facts.GstPan = facts.GstGstin.Substring(2, 10);
                        }
                        facts.GstName = Text(data, "tradeNam") ?? Text(data, "lgnm");
                    }
                }
                catch (JsonException) { }
            }

            if (!string.IsNullOrWhiteSpace(itrJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(itrJson);
                    var data = At(doc.RootElement, "data");
                    if (data.ValueKind != JsonValueKind.Object) data = doc.RootElement;
                    if (data.ValueKind == JsonValueKind.Object)
                    {
                        var ids = At(data, "source_identifiers");
                        if (ids.ValueKind == JsonValueKind.Object)
                        {
                            facts.ItrPan = Text(ids, "pan");
                            facts.ItrGstin = Text(ids, "gstin");
                        }
                        var entity = At(data, "entity_details");
                        if (entity.ValueKind == JsonValueKind.Object)
                        {
                            facts.ItrName = Text(entity, "trade_name") ?? Text(entity, "firm_name") ?? Text(entity, "proprietor_name");
                        }
                    }
                }
                catch (JsonException) { }
            }

            if (!string.IsNullOrWhiteSpace(mcaJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(mcaJson);
                    var root = doc.RootElement;
                    var msg = At(root, "message");
                    if (msg.ValueKind != JsonValueKind.Object) msg = root;
                    facts.McaName = Text(msg, "company_name");

                    var details = At(msg, "details");
                    var info = At(details, "company_info");
                    if (info.ValueKind == JsonValueKind.Object)
                    {
                        facts.McaStatus = Text(info, "company_status");
                    }
                }
                catch (JsonException) { }
            }

            return facts;
        }

        private static JsonElement At(JsonElement e, params string[] path)
        {
            foreach (var p in path)
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return default;
            return e;
        }

        private static string? Text(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>
        /// Task T4 (Doc/13_TEAM_WORK_PLAN.md): Identity check verifying PAN, GSTIN, and business name agreement
        /// across official sources, and that MCA company status is Active.
        /// </summary>
        public static List<Finding> Identity(IdentityFacts facts, int loanApplicationId)
        {
            if (facts == null) return new();
            var list = new List<Finding>();

            // Rule 1: PAN differs between Udyam and ITR, or between Udyam and GST -> IDENTITY_MISMATCH, High
            if (!string.IsNullOrWhiteSpace(facts.UdyamPan) && !string.IsNullOrWhiteSpace(facts.ItrPan)
                && !string.Equals(facts.UdyamPan.Trim(), facts.ItrPan.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Finding
                {
                    LoanApplicationID = loanApplicationId,
                    Type = "IDENTITY_MISMATCH",
                    Category = FindingCategory.CrossDocument,
                    Severity = FindingSeverity.High,
                    Title = "Identity mismatch: PAN differs between Udyam and ITR",
                    Explanation = $"Udyam registration reports PAN {facts.UdyamPan.Trim()}, but filed ITR reports PAN {facts.ItrPan.Trim()}.",
                    RecommendedAction = "Verify original PAN document and determine whether the tax return belongs to this entity.",
                    DetectedBy = FindingSource.Deterministic,
                    DedupKey = "IDENTITY_MISMATCH",
                    Evidence = new()
                    {
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"Udyam PAN: {facts.UdyamPan.Trim()}" },
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"ITR PAN: {facts.ItrPan.Trim()}" }
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(facts.UdyamPan) && !string.IsNullOrWhiteSpace(facts.GstPan)
                && !string.Equals(facts.UdyamPan.Trim(), facts.GstPan.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Finding
                {
                    LoanApplicationID = loanApplicationId,
                    Type = "IDENTITY_MISMATCH",
                    Category = FindingCategory.CrossDocument,
                    Severity = FindingSeverity.High,
                    Title = "Identity mismatch: PAN differs between Udyam and GST",
                    Explanation = $"Udyam registration reports PAN {facts.UdyamPan.Trim()}, but GST profile reports PAN {facts.GstPan.Trim()}.",
                    RecommendedAction = "Verify business registration documents and confirm why GST was obtained under a different PAN.",
                    DetectedBy = FindingSource.Deterministic,
                    DedupKey = "IDENTITY_MISMATCH",
                    Evidence = new()
                    {
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"Udyam PAN: {facts.UdyamPan.Trim()}" },
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"GST PAN: {facts.GstPan.Trim()}" }
                    }
                });
            }

            // Rule 2: GSTIN on Udyam differs from the GST record -> IDENTITY_MISMATCH, High
            if (!string.IsNullOrWhiteSpace(facts.UdyamGstin) && !string.IsNullOrWhiteSpace(facts.GstGstin)
                && !string.Equals(facts.UdyamGstin.Trim(), facts.GstGstin.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new Finding
                {
                    LoanApplicationID = loanApplicationId,
                    Type = "IDENTITY_MISMATCH",
                    Category = FindingCategory.CrossDocument,
                    Severity = FindingSeverity.High,
                    Title = "Identity mismatch: GSTIN differs between Udyam and GST",
                    Explanation = $"Udyam registration reports GSTIN {facts.UdyamGstin.Trim()}, but GST taxpayer record reports GSTIN {facts.GstGstin.Trim()}.",
                    RecommendedAction = "Review GST registration certificate to confirm active GSTIN of the enterprise.",
                    DetectedBy = FindingSource.Deterministic,
                    DedupKey = "IDENTITY_MISMATCH",
                    Evidence = new()
                    {
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"Udyam GSTIN: {facts.UdyamGstin.Trim()}" },
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"GST profile GSTIN: {facts.GstGstin.Trim()}" }
                    }
                });
            }

            // Rule 3: Business name differs after normalising -> IDENTITY_MISMATCH, Medium
            var namePairs = new List<(string Label1, string Name1, string Label2, string Name2)>();
            if (!string.IsNullOrWhiteSpace(facts.UdyamName))
            {
                if (!string.IsNullOrWhiteSpace(facts.GstName)) namePairs.Add(("Udyam", facts.UdyamName, "GST", facts.GstName));
                if (!string.IsNullOrWhiteSpace(facts.ItrName)) namePairs.Add(("Udyam", facts.UdyamName, "ITR", facts.ItrName));
                if (!string.IsNullOrWhiteSpace(facts.McaName)) namePairs.Add(("Udyam", facts.UdyamName, "MCA", facts.McaName));
            }
            else if (!string.IsNullOrWhiteSpace(facts.GstName) && !string.IsNullOrWhiteSpace(facts.ItrName))
            {
                namePairs.Add(("GST", facts.GstName, "ITR", facts.ItrName));
            }

            foreach (var pair in namePairs)
            {
                if (!BusinessNamesMatch(pair.Name1, pair.Name2))
                {
                    list.Add(new Finding
                    {
                        LoanApplicationID = loanApplicationId,
                        Type = "IDENTITY_MISMATCH",
                        Category = FindingCategory.CrossDocument,
                        Severity = FindingSeverity.Medium,
                        Title = "Identity mismatch: business name differs across sources",
                        Explanation = $"Enterprise name in {pair.Label1} ('{pair.Name1}') differs from {pair.Label2} ('{pair.Name2}') after normalisation.",
                        RecommendedAction = "Review trade name variations or investigate whether recent change in entity trade name occurred.",
                        DetectedBy = FindingSource.Deterministic,
                        DedupKey = "IDENTITY_MISMATCH",
                        Evidence = new()
                        {
                            new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"{pair.Label1} name: {pair.Name1}" },
                            new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"{pair.Label2} name: {pair.Name2}" }
                        }
                    });
                    break;
                }
            }

            // Rule 4: MCA company status is not Active -> COMPANY_INACTIVE, High
            if (!string.IsNullOrWhiteSpace(facts.McaStatus)
                && !string.Equals(facts.McaStatus.Trim(), "Active", StringComparison.OrdinalIgnoreCase)
                && facts.McaStatus.Trim() != "-")
            {
                list.Add(new Finding
                {
                    LoanApplicationID = loanApplicationId,
                    Type = "COMPANY_INACTIVE",
                    Category = FindingCategory.CrossDocument,
                    Severity = FindingSeverity.High,
                    Title = $"MCA corporate status is {facts.McaStatus.Trim()}",
                    Explanation = $"Ministry of Corporate Affairs reports company status as '{facts.McaStatus.Trim()}' (expected 'Active'). Companies that are struck off, dormant, or under liquidation are ineligible for credit.",
                    RecommendedAction = "Verify current MCA filings, active status compliance (INC-20A), and registrar orders before proceeding.",
                    DetectedBy = FindingSource.Deterministic,
                    DedupKey = "COMPANY_INACTIVE",
                    Evidence = new()
                    {
                        new Evidence { Kind = EvidenceKind.App, RefId = loanApplicationId, Note = $"MCA company status: {facts.McaStatus.Trim()}" }
                    }
                });
            }

            // One finding per pattern, at its most severe
            return list
                .GroupBy(f => f.DedupKey)
                .Select(g => g.OrderBy(f => f.Severity == FindingSeverity.High ? 0 : 1).First())
                .ToList();
        }

        public static List<Finding> Identity(string? udyamJson, string? gstTaxpayerJson, string? itrJson, string? mcaJson, int loanApplicationId)
            => Identity(ParseIdentityFacts(udyamJson, gstTaxpayerJson, itrJson, mcaJson), loanApplicationId);

        private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
