using System.Globalization;
using FinRiskLensAI.Core.Models.Scoring;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.ML.Features
{
    /// <summary>
    /// Extracts income, filing-discipline and financial-ratio features from the ITR
    /// vendor response (<c>itr.json</c> in the MSME's blob folder, produced by
    /// <c>DummyDataService.GetDummyItr</c>).
    /// <para>
    /// The response carries <b>one assessment year</b> under <c>data</c>, in one of two
    /// shapes: ITR-4 (SUGAM) presumptive for proprietorships, or ITR-5 books-of-account
    /// for partnerships / LLPs / companies. Both are handled here; whichever fields the
    /// shape doesn't carry stay null rather than being back-filled with a guess.
    /// </para>
    /// <para>
    /// A single year means no income trend can be measured — <see cref="MsmeFeatureSet.ItrIncomeTrendSlope"/>
    /// stays 0 (neutral), and the compliance dimension reads the filing-quality flags below
    /// instead of a years-filed count that can only ever be 1. The yearly-income dictionary
    /// is kept so that a multi-year response later needs no change here.
    /// </para>
    /// </summary>
    public class ItrFeatureExtractor
    {
        public void Extract(string itrJson, MsmeFeatureSet features)
        {
            if (string.IsNullOrWhiteSpace(itrJson)) return;

            JObject root;
            try { root = JObject.Parse(itrJson); }
            catch (Newtonsoft.Json.JsonException) { return; }

            // The vendor reports failures in-band; a non-SUCCESS body carries no return.
            var status = root.Value<string>("status");
            if (!string.IsNullOrWhiteSpace(status)
                && !status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)) return;

            if (root["data"] is not JObject data) return;

            features.HasItr = true;

            ExtractFiling(data, features);
            ExtractIncome(data, features);
            ExtractFinancials(data, features);
            ExtractReconciliation(data, features);
        }

        // ─────────────────────────── filing discipline ───────────────────────────

        private static void ExtractFiling(JObject data, MsmeFeatureSet features)
        {
            var filing = data["filing_details"] as JObject;
            var audit = data["audit_details"] as JObject;
            var tax = data["tax_computation"] as JObject;

            features.ItrFormType = filing?.Value<string>("itr_form_type");
            features.ItrAssessmentYear = filing?.Value<string>("assessment_year");
            features.ItrFilingSection = filing?.Value<string>("filing_section");
            features.ItrIsPresumptive = features.ItrFormType?.Contains("ITR-4", StringComparison.OrdinalIgnoreCase) == true
                                        || data["presumptive_income_details"] != null;

            features.ItrEVerified = string.Equals(filing?.Value<string>("e_verification_status"),
                "VERIFIED", StringComparison.OrdinalIgnoreCase);
            features.ItrReturnProcessed = string.Equals(filing?.Value<string>("return_status"),
                "PROCESSED", StringComparison.OrdinalIgnoreCase);

            features.ItrAuditApplicable = audit?.Value<bool?>("is_tax_audit_applicable") == true;
            features.ItrAuditCompleted = features.ItrAuditApplicable
                && !string.IsNullOrWhiteSpace(audit?.Value<string>("form_3ca_3cb_filing_date"));

            features.ItrLateFilingInterest = Number(tax, "interest_us_234a") ?? 0;
            features.ItrAdvanceTaxInterest = (Number(tax, "interest_us_234b") ?? 0)
                                           + (Number(tax, "interest_us_234c") ?? 0);

            var demandType = tax?["refund_or_demand"]?.Value<string>("type");
            features.ItrHasTaxDemand = string.Equals(demandType, "DEMAND", StringComparison.OrdinalIgnoreCase);

            var payable = Number(tax, "total_tax_and_interest") ?? 0;
            var paid = Number(tax?["taxes_paid"] as JObject, "total_taxes_paid") ?? 0;
            // Nothing to pay is full discharge, not a missing signal.
            features.ItrTaxPaidRatio = payable <= 0 ? 1 : Math.Clamp(paid / payable, 0, 1);

            features.ItrFilingTimeliness = FilingTimeliness(filing, features);
            features.ItrYearsFiled = 1;
        }

        /// <summary>
        /// 1 when the return was filed by its statutory due date, 0 when it was late.
        /// <para>
        /// Three independent signals agree in a clean return, so any one of them failing
        /// marks it late: the filing section (139(1) is the on-time section, 139(4) is
        /// belated), the filing date against the due date, and interest u/s 234A — which
        /// the department charges <i>only</i> for a late return.
        /// </para>
        /// </summary>
        private static double FilingTimeliness(JObject? filing, MsmeFeatureSet features)
        {
            if (filing == null) return 0.5;                       // unknown, not bad

            if (features.ItrLateFilingInterest > 0) return 0;     // 234A only arises on a late return

            var section = features.ItrFilingSection?.Replace(" ", "");
            if (!string.IsNullOrWhiteSpace(section))
            {
                if (section.Contains("139(4)")) return 0;         // belated
                if (section.Contains("139(1)") || section.Contains("139(5)"))
                {
                    // 139(5) revises an original that was itself on time.
                    return FiledByDueDate(filing, features) switch { false => 0, _ => 1 };
                }
            }

            return FiledByDueDate(filing, features) switch { true => 1, false => 0, null => 0.5 };
        }

        /// <summary>
        /// Filing date against the due date for the assessment year: 31 October for a
        /// return under tax audit, 31 July otherwise. Null when either date is unreadable.
        /// </summary>
        private static bool? FiledByDueDate(JObject filing, MsmeFeatureSet features)
        {
            var filed = ParseDate(filing.Value<string>("filing_date"));
            if (filed == null) return null;

            var ayStart = AssessmentYearStart(features.ItrAssessmentYear);
            if (ayStart == null) return null;

            var dueDate = features.ItrAuditApplicable
                ? new DateTime(ayStart.Value, 10, 31)
                : new DateTime(ayStart.Value, 7, 31);

            return filed.Value.Date <= dueDate;
        }

        /// <summary>"2025-26" → 2025. The AY is the year the return is filed in.</summary>
        private static int? AssessmentYearStart(string? assessmentYear)
            => int.TryParse(assessmentYear?.Split('-').FirstOrDefault(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var y) && y > 1900 ? y : null;

        // ─────────────────────────── income ───────────────────────────

        private static void ExtractIncome(JObject data, MsmeFeatureSet features)
        {
            var income = data["income_details"] as JObject;

            // Gross total income is the comparable line across both form shapes.
            var gross = Number(income, "gross_total_income") ?? Number(data, "total_income");
            if (gross == null) return;

            var yearKey = features.ItrAssessmentYear ?? "unknown";
            features.ItrYearlyIncome[yearKey] = gross.Value;
            features.ItrLatestIncome = gross.Value;
            features.ItrMonthlyAvgIncome = gross.Value / 12.0;

            // One assessment year carries no trend. NormalizedSlope returns 0 for a
            // single point, which is the neutral value the score expects — set it
            // explicitly so a future multi-year response is the only thing that moves it.
            features.ItrIncomeTrendSlope = TrendMath.NormalizedSlope(features.ItrYearlyIncome.Values.ToArray());
        }

        // ─────────────────────────── financial ratios ───────────────────────────

        /// <summary>
        /// Turnover, margins and balance-sheet ratios. ITR-5 reports a real P&amp;L and
        /// balance sheet; ITR-4 presumptive filers keep no books, so only the turnover
        /// split and the summary "no account case" figures are available there.
        /// Ratios stay null when the return doesn't carry the source figure.
        /// </summary>
        private static void ExtractFinancials(JObject data, MsmeFeatureSet features)
        {
            var presumptive = data["presumptive_income_details"] as JObject;
            var pl = data["profit_and_loss_summary"] as JObject;
            var bs = data["balance_sheet_summary"] as JObject;

            var turnover = Number(pl, "revenue_from_operations")
                        ?? Number(presumptive, "gross_turnover_or_gross_receipts");
            if (turnover is not > 0) return;

            features.ItrFinancialsYear = features.ItrAssessmentYear;
            features.ItrBusinessTurnover = turnover.Value;

            // ── margins (books filers only — a presumptive return declares a flat rate,
            //    not a measured margin, so reporting one would be fiction)
            if (pl != null)
            {
                var pbt = Number(pl, "net_profit_before_tax");
                if (pbt.HasValue)
                    features.ItrPbtMargin = Math.Round(pbt.Value / turnover.Value, 4);

                var pat = Number(pl, "net_profit_after_tax");
                if (pat.HasValue)
                    features.ItrNetProfitMargin = Math.Round(pat.Value / turnover.Value, 4);
            }

            // ItrEbitdaMargin stays null: this response has no PBIDTA / depreciation
            // line, and PBT is not EBITDA. ItrPbtMargin carries the operating signal.

            // ── balance sheet
            var totalAssets = Number(bs, "total_assets");
            if (totalAssets is > 0)
            {
                features.ItrTotalAssets = totalAssets.Value;
                features.ItrAssetTurnover = Math.Round(turnover.Value / totalAssets.Value, 2);
            }

            var capital = Number(bs, "total_partners_capital");
            if (capital is > 0)
            {
                features.ItrNetWorth = capital.Value;

                // total_liabilities is the liabilities side including capital, so the
                // outside-debt figure is what's left after capital is removed.
                var totalLiabilities = Number(bs, "total_liabilities");
                if (totalLiabilities.HasValue)
                {
                    var outsideDebt = Math.Max(0, totalLiabilities.Value - capital.Value);
                    features.ItrDebtToEquity = Math.Round(outsideDebt / capital.Value, 2);
                }
            }

            // ── presumptive-only signals
            if (presumptive != null)
            {
                var cash = Number(presumptive, "turnover_in_cash");
                if (cash.HasValue)
                    features.ItrCashTurnoverShare = Math.Clamp(cash.Value / turnover.Value, 0, 1);

                // No books means no balance sheet; the return still summarises debtors.
                var debtors = Number(data["no_account_case_financials"] as JObject, "total_sundry_debtors");
                if (debtors is > 0)
                    features.ItrDebtorDays = Math.Round(debtors.Value / turnover.Value * 365, 1);
            }
        }

        // ─────────────────────────── GST cross-check ───────────────────────────

        private static void ExtractReconciliation(JObject data, MsmeFeatureSet features)
        {
            var recon = data["gst_turnover_reconciliation"] as JObject;
            if (recon == null) return;

            var variance = Number(recon, "variance_percent");
            if (variance.HasValue)
            {
                features.ItrGstTurnoverVariancePct = Math.Abs(variance.Value);
                return;
            }

            // Fall back to computing it when the vendor sends the amounts but no percent.
            var gst = Number(recon, "gst_annual_turnover_reported");
            var itr = Number(recon, "itr_declared_turnover");
            if (gst.HasValue && itr is > 0)
                features.ItrGstTurnoverVariancePct = Math.Round(Math.Abs(gst.Value - itr.Value) / itr.Value * 100, 2);
        }

        // ─────────────────────────── helpers ───────────────────────────

        /// <summary>Reads a direct child as a number. Null when absent, null-valued or non-numeric.</summary>
        private static double? Number(JObject? scope, string property)
        {
            var token = scope?[property];
            if (token == null || token.Type == JTokenType.Null) return null;
            return double.TryParse(token.ToString(), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var value) ? value : null;
        }

        private static DateTime? ParseDate(string? value)
            => DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var d) ? d : null;
    }
}
