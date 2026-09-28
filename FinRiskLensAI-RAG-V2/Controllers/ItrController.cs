using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Models.Itr;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Income Tax Return screens for the logged-in MSME. The source is the ITR
    /// vendor response stored as <c>itr.json</c> in the MSME's blob folder (seeded
    /// by <c>DashboardController.SeedItrData</c>), read back through
    /// <see cref="CustomerProfileBuilder"/> — the same path the Udyam, MCA and AA
    /// pages use. The UAN always comes from the authenticated session, never from
    /// the request.
    /// <para>
    /// The response is parsed here rather than in
    /// <c>FinRiskLensAI.ML.Features.ItrFeatureExtractor</c>: that one produces the
    /// numeric feature vector the score consumes, this one produces the presentation
    /// model. They read the same JSON and must agree — the due-date rule and the
    /// filing-quality weights below deliberately mirror
    /// <c>RiskScoringService.ItrCompliance</c>.
    /// </para>
    /// </summary>
    [CustDashboardAuthorize]
    public class ItrController : Controller
    {
        private readonly CustomerProfileBuilder _profile;

        public ItrController(CustomerProfileBuilder profile) => _profile = profile;

        /// <summary>Income Tax Return analysis page.</summary>
        [HttpGet]
        public async Task<IActionResult> ItrDetails(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetItrAsync(uan, ct));
        }

        // ─────────────────────────── analysis ───────────────────────────

        /// <summary>
        /// Builds the page model from the raw vendor response. Shared with the bank
        /// portal via <see cref="CustomerProfileBuilder.GetItrAsync"/>, so both
        /// surfaces render identical figures from one code path.
        /// </summary>
        internal static ItrDetailsViewModel BuildAnalysis(JObject root, string? sessionUan)
        {
            var data = root["data"] as JObject;
            var model = new ItrDetailsViewModel { Uan = sessionUan };
            if (data == null)
            {
                model.LoadError = "The stored ITR response could not be read.";
                return model;
            }

            model.HasData = true;

            var ids = data["source_identifiers"] as JObject;
            var entity = data["entity_details"] as JObject;
            var filing = data["filing_details"] as JObject;
            var income = data["income_details"] as JObject;
            var tax = data["tax_computation"] as JObject;
            var presumptive = data["presumptive_income_details"] as JObject;
            var pl = data["profit_and_loss_summary"] as JObject;
            var bs = data["balance_sheet_summary"] as JObject;
            var audit = data["audit_details"] as JObject;
            var recon = data["gst_turnover_reconciliation"] as JObject;

            BuildEntity(model, ids, entity, sessionUan);
            BuildFiling(model, filing, audit, data);
            BuildIncome(model, income, data);
            BuildTax(model, tax);
            BuildPresumptive(model, presumptive);
            BuildFinancials(model, pl, bs);
            BuildPartners(model, data);
            BuildReconciliation(model, recon);
            BuildAudit(model, audit);
            BuildBankAndVerification(model, data);
            BuildScore(model);

            return model;
        }

        private static void BuildEntity(ItrDetailsViewModel m, JObject? ids, JObject? entity, string? sessionUan)
        {
            m.Uan = Str(ids, "udyam_registration_number") ?? sessionUan;
            m.Pan = Str(ids, "pan") ?? "-";
            m.Gstin = Str(ids, "gstin") ?? "-";
            m.PanMasked = Universal.MaskPan(m.Pan) ?? m.Pan;
            m.GstinMasked = Universal.MaskGstin(m.Gstin) ?? m.Gstin;

            // ITR-5 reports firm_name, ITR-4 reports proprietor_name / trade_name.
            m.EntityName = Str(entity, "firm_name") ?? Str(entity, "trade_name")
                        ?? Str(entity, "proprietor_name") ?? "-";
            m.ConstitutionType = Pretty(Str(entity, "constitution_type"));
            m.Llpin = Str(entity, "llpin");
            m.Email = Str(entity, "email") ?? "-";
            m.Mobile = Str(entity, "mobile") ?? "-";
            m.DateOfFormationText = DateText(Str(entity, "date_of_formation")
                                          ?? Str(entity, "date_of_commencement"));

            var addr = entity?["registered_address"] as JObject;
            var parts = new[] { Str(addr, "line1"), Str(addr, "city"), Str(addr, "state"), Str(addr, "pincode") }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            m.RegisteredAddress = parts.Any() ? string.Join(", ", parts) : "-";
        }

        private static void BuildFiling(ItrDetailsViewModel m, JObject? filing, JObject? audit, JObject data)
        {
            m.FormType = Str(filing, "itr_form_type") ?? "-";
            m.AssessmentYear = Str(filing, "assessment_year") ?? "-";
            m.FinancialYear = Str(filing, "financial_year") ?? "-";
            m.FilingType = Pretty(Str(filing, "filing_type"));
            m.FilingSection = Str(filing, "filing_section") ?? "-";
            m.AcknowledgementNumber = Str(filing, "acknowledgement_number") ?? "-";
            m.FilingDateText = DateText(Str(filing, "filing_date"));
            m.FilingMode = Str(filing, "filing_mode") ?? "-";
            m.EVerificationStatus = Pretty(Str(filing, "e_verification_status"));
            m.EVerificationDateText = DateText(Str(filing, "e_verification_date"));
            m.ReturnStatus = Pretty(Str(filing, "return_status"));

            m.IsPresumptive = m.FormType.Contains("ITR-4", StringComparison.OrdinalIgnoreCase)
                              || data["presumptive_income_details"] != null;
            m.EVerified = string.Equals(Str(filing, "e_verification_status"), "VERIFIED", StringComparison.OrdinalIgnoreCase);
            m.ReturnProcessed = string.Equals(Str(filing, "return_status"), "PROCESSED", StringComparison.OrdinalIgnoreCase);
            m.AuditApplicable = Bool(audit, "is_tax_audit_applicable");

            // Due date: 31 October for a return under tax audit, 31 July otherwise.
            var ayStart = AssessmentYearStart(m.AssessmentYear);
            if (ayStart.HasValue)
            {
                var due = m.AuditApplicable ? new DateTime(ayStart.Value, 10, 31) : new DateTime(ayStart.Value, 7, 31);
                m.DueDateText = due.ToString("dd MMM yyyy");

                var filed = ParseDate(Str(filing, "filing_date"));
                var section = m.FilingSection.Replace(" ", "");
                var interest234A = Num(data["tax_computation"] as JObject, "interest_us_234a") ?? 0;

                // Three signals must agree; 234A interest is charged only on a late return.
                m.FiledOnTime = filed.HasValue
                                && filed.Value.Date <= due
                                && !section.Contains("139(4)")
                                && interest234A <= 0;
            }
        }

        private static void BuildIncome(ItrDetailsViewModel m, JObject? income, JObject data)
        {
            m.BusinessIncome = L(Num(income, "profits_and_gains_of_business_or_profession")
                                 ?? Num(income, "income_from_business_presumptive"));
            m.HousePropertyIncome = L(Num(income, "income_from_house_property"));
            m.CapitalGains = L(Num(income, "capital_gains"));
            m.OtherSourcesIncome = L(Num(income, "income_from_other_sources"));
            m.GrossTotalIncome = L(Num(income, "gross_total_income"));
            m.TotalIncome = L(Num(data, "total_income"));

            var ded = data["deductions_chapter_via"] as JObject;
            m.TotalDeductions = L(Num(ded, "total_deductions"));
            foreach (var p in ded?.Properties() ?? Enumerable.Empty<JProperty>())
            {
                if (p.Name == "total_deductions") continue;
                var amount = L(Num(ded, p.Name));
                if (amount <= 0) continue;
                m.DeductionRows.Add(new ItrDetailsViewModel.ItrAmountRow
                {
                    // "section_80g" -> "Section 80G"
                    Label = Pretty(p.Name).Replace("Section 80", "Section 80").ToUpperInvariant()
                                .Replace("SECTION", "Section"),
                    Amount = amount,
                    AmountText = Money(amount)
                });
            }
        }

        private static void BuildTax(ItrDetailsViewModel m, JObject? tax)
        {
            var rate = Num(tax, "tax_rate_applied_percent");
            m.TaxRateText = rate.HasValue ? $"{rate.Value:0.##}%" : "Slab rates";
            m.TaxOnTotalIncome = L(Num(tax, "tax_on_total_income"));
            m.Surcharge = L(Num(tax, "surcharge"));
            m.Cess = L(Num(tax, "health_and_education_cess"));
            m.TotalTaxLiability = L(Num(tax, "total_tax_liability"));
            m.Interest234A = L(Num(tax, "interest_us_234a"));
            m.Interest234B = L(Num(tax, "interest_us_234b"));
            m.Interest234C = L(Num(tax, "interest_us_234c"));
            m.TotalTaxAndInterest = L(Num(tax, "total_tax_and_interest"));

            var paid = tax?["taxes_paid"] as JObject;
            m.AdvanceTax = L(Num(paid, "advance_tax"));
            m.Tds = L(Num(paid, "tds"));
            m.Tcs = L(Num(paid, "tcs"));
            m.SelfAssessmentTax = L(Num(paid, "self_assessment_tax"));
            m.TotalTaxesPaid = L(Num(paid, "total_taxes_paid"));

            var rd = tax?["refund_or_demand"] as JObject;
            m.RefundOrDemandType = Str(rd, "type") ?? "NIL";
            m.RefundOrDemandAmount = L(Num(rd, "amount"));
            m.HasTaxDemand = string.Equals(m.RefundOrDemandType, "DEMAND", StringComparison.OrdinalIgnoreCase);

            // Nothing to pay is full discharge, not a missing signal.
            m.TaxPaidRatio = m.TotalTaxAndInterest <= 0
                ? 1
                : Math.Clamp((double)m.TotalTaxesPaid / m.TotalTaxAndInterest, 0, 1);
        }

        private static void BuildPresumptive(ItrDetailsViewModel m, JObject? p)
        {
            if (p == null) return;
            m.HasPresumptive = true;
            m.SchemeSection = Str(p, "scheme_section") ?? "-";
            m.NatureOfBusiness = Str(p, "nature_of_business_code") ?? "-";
            m.PresumptiveTurnover = L(Num(p, "gross_turnover_or_gross_receipts"));
            m.TurnoverBanking = L(Num(p, "turnover_through_banking_channels"));
            m.TurnoverCash = L(Num(p, "turnover_in_cash"));
            m.PresumptiveIncomeDeclared = L(Num(p, "presumptive_income_declared"));
            var rate = Num(p, "effective_rate_declared_percent");
            m.EffectiveRateText = rate.HasValue ? $"{rate.Value:0.##}%" : "-";
            if (m.PresumptiveTurnover > 0)
                m.CashTurnoverSharePct = Math.Round((double)m.TurnoverCash / m.PresumptiveTurnover * 100, 1);
        }

        private static void BuildFinancials(ItrDetailsViewModel m, JObject? pl, JObject? bs)
        {
            if (pl != null)
            {
                m.HasFinancials = true;
                m.PeriodText = Str(pl, "period") ?? "-";
                m.RevenueFromOperations = L(Num(pl, "revenue_from_operations"));
                m.OtherIncome = L(Num(pl, "other_income"));
                m.TotalExpenses = L(Num(pl, "total_expenses"));
                m.NetProfitBeforeTax = L(Num(pl, "net_profit_before_tax"));
                m.NetProfitAfterTax = L(Num(pl, "net_profit_after_tax"));

                if (m.RevenueFromOperations > 0)
                {
                    m.PbtMarginPct = Math.Round((double)m.NetProfitBeforeTax / m.RevenueFromOperations * 100, 2);
                    m.NetMarginPct = Math.Round((double)m.NetProfitAfterTax / m.RevenueFromOperations * 100, 2);
                }
            }

            if (bs == null) return;
            m.HasBalanceSheet = true;
            m.BalanceSheetDateText = DateText(Str(bs, "as_on_date"));
            m.TotalPartnersCapital = L(Num(bs, "total_partners_capital"));
            m.TotalLiabilities = L(Num(bs, "total_liabilities"));
            m.TotalAssets = L(Num(bs, "total_assets"));

            var turnover = m.RevenueFromOperations > 0 ? m.RevenueFromOperations : m.PresumptiveTurnover;
            if (m.TotalAssets > 0 && turnover > 0)
                m.AssetTurnover = Math.Round((double)turnover / m.TotalAssets, 2);

            // total_liabilities is the liabilities side including capital, so the
            // outside-debt figure is what is left once capital is removed.
            if (m.TotalPartnersCapital > 0)
                m.DebtToEquity = Math.Round(
                    Math.Max(0, (double)(m.TotalLiabilities - m.TotalPartnersCapital)) / m.TotalPartnersCapital, 2);
        }

        private static void BuildPartners(ItrDetailsViewModel m, JObject data)
        {
            foreach (var p in data["partners_details"] as JArray ?? new JArray())
            {
                var o = p as JObject;
                var name = Str(o, "name") ?? "-";
                var pan = Str(o, "pan") ?? "-";
                m.Partners.Add(new ItrDetailsViewModel.ItrPartnerItem
                {
                    Name = name,
                    Initials = Initials(name),
                    Pan = pan,
                    PanMasked = Universal.MaskPan(pan) ?? pan,
                    SharePercent = Num(o, "profit_sharing_ratio_percent") ?? 0,
                    CapitalBalance = L(Num(o, "capital_balance_as_on_year_end")),
                    CapitalText = Money(L(Num(o, "capital_balance_as_on_year_end"))),
                    IsWorkingPartner = Bool(o, "is_working_partner")
                });
            }

            var rem = data["partner_remuneration_and_interest"] as JObject;
            m.TotalRemuneration = L(Num(rem, "total_remuneration_paid_to_partners"));
            m.TotalInterestOnCapital = L(Num(rem, "total_interest_on_capital_paid"));
            m.Allowable40b = L(Num(rem, "allowable_under_section_40b"));
            m.Disallowed40b = L(Num(rem, "amount_disallowed"));
        }

        private static void BuildReconciliation(ItrDetailsViewModel m, JObject? recon)
        {
            if (recon == null) return;
            m.HasGstReconciliation = true;
            m.GstReportedTurnover = L(Num(recon, "gst_annual_turnover_reported"));
            m.ItrDeclaredTurnover = L(Num(recon, "itr_declared_turnover"));
            m.VarianceAmount = L(Num(recon, "variance_amount"));
            m.VariancePercent = Num(recon, "variance_percent")
                ?? (m.ItrDeclaredTurnover > 0
                    ? Math.Round(Math.Abs((double)m.VarianceAmount) / m.ItrDeclaredTurnover * 100, 2)
                    : 0);
            m.ReconciliationStatus = Pretty(Str(recon, "reconciliation_status"));
            m.ReconciliationWithinTolerance = Math.Abs(m.VariancePercent) <= 5;
        }

        private static void BuildAudit(ItrDetailsViewModel m, JObject? audit)
        {
            if (audit == null) return;
            m.AuditSection = Str(audit, "audit_section") ?? "-";
            m.AuditorName = Str(audit, "auditor_name") ?? "-";
            m.AuditorMembership = Str(audit, "auditor_membership_number") ?? "-";
            m.Udin = Str(audit, "udin") ?? "-";
            var filed = Str(audit, "form_3ca_3cb_filing_date");
            m.AuditFilingDateText = DateText(filed);
            m.AuditCompleted = m.AuditApplicable && !string.IsNullOrWhiteSpace(filed);
        }

        private static void BuildBankAndVerification(ItrDetailsViewModel m, JObject data)
        {
            foreach (var b in data["bank_details"] as JArray ?? new JArray())
            {
                var o = b as JObject;
                m.BankAccounts.Add(new ItrDetailsViewModel.ItrBankItem
                {
                    BankName = Str(o, "bank_name") ?? "-",
                    AccountMasked = Str(o, "account_number_masked") ?? "-",
                    Ifsc = Str(o, "ifsc_code") ?? "-",
                    AccountType = Str(o, "account_type") ?? "-",
                    IsRefundAccount = Bool(o, "is_refund_account")
                });
            }

            var v = data["verification"] as JObject;
            m.VerifiedByName = Str(v, "verified_by_name") ?? "-";
            m.VerifierDesignation = Str(v, "designation") ?? "-";
            m.VerificationDateText = DateText(Str(v, "verification_date"));
            m.VerificationPlace = Str(v, "place") ?? "-";
        }

        /// <summary>
        /// Filing &amp; tax health, 0–100. The weights mirror
        /// <c>RiskScoringService.ItrCompliance</c> so this page and the credit score
        /// never tell the borrower two different stories; the extra profitability and
        /// GST-reconciliation components are presentation-only and carry no weight in
        /// the 1000-point score.
        /// </summary>
        private static void BuildScore(ItrDetailsViewModel m)
        {
            void Component(string name, int points, int max, string reason)
            {
                var pct = max > 0 ? (int)Math.Round(points * 100.0 / max) : 0;
                m.ScoreComponents.Add(new ItrDetailsViewModel.ItrScoreComponent
                {
                    Name = name,
                    Points = points,
                    MaxPoints = max,
                    Percent = pct,
                    Reason = reason,
                    FillCss = pct >= 70 ? "fill-good" : pct >= 45 ? "fill-warn" : "fill-bad"
                });
            }

            Component("Filing Timeliness", m.FiledOnTime ? 30 : 0, 30,
                m.FiledOnTime
                    ? $"Filed {m.FilingDateText} under section {m.FilingSection}, on or before the {m.DueDateText} due date."
                    : $"Filed after the {m.DueDateText} due date, or under the belated section 139(4).");

            Component("E-Verification", m.EVerified ? 15 : 0, 15,
                m.EVerified
                    ? $"Return e-verified on {m.EVerificationDateText}."
                    : "Return is not e-verified — an unverified return is treated as never filed.");

            Component("Processing Status", m.ReturnProcessed ? 15 : 0, 15,
                m.ReturnProcessed
                    ? "Accepted and processed by CPC."
                    : $"Return status is {m.ReturnStatus} — not yet processed by CPC.");

            var taxPoints = (int)Math.Round(m.TaxPaidRatio * 25);
            Component("Tax Discharged", taxPoints, 25,
                m.TotalTaxAndInterest <= 0
                    ? "No tax was payable for this assessment year."
                    : $"{Money(m.TotalTaxesPaid)} paid against {Money(m.TotalTaxAndInterest)} due ({m.TaxPaidRatio:P0}).");

            var auditPoints = !m.AuditApplicable ? 15 : m.AuditCompleted ? 15 : 0;
            Component("Audit Compliance", auditPoints, 15,
                !m.AuditApplicable
                    ? "Turnover is below the section 44AB tax-audit threshold."
                    : m.AuditCompleted
                        ? $"Tax audit under {m.AuditSection} completed; Form 3CA/3CB filed {m.AuditFilingDateText}."
                        : $"Tax audit under {m.AuditSection} is applicable but Form 3CA/3CB has not been filed.");

            var raw = m.ScoreComponents.Sum(c => c.Points);

            // Penalties, same direction as the ML compliance sub-score.
            if (m.HasTaxDemand) raw -= 10;
            if (m.Interest234B + m.Interest234C > 0) raw -= 5;

            m.Score = Math.Clamp(raw, 0, 100);
            (m.RiskLabel, m.RiskCss) = m.Score >= 80 ? ("LOW", "risk-low")
                                     : m.Score >= 60 ? ("MEDIUM", "risk-medium")
                                     : ("HIGH", "risk-high");

            BuildObservations(m);
            BuildVerificationMatrix(m);
        }

        private static void BuildObservations(ItrDetailsViewModel m)
        {
            void Add(string text, string css = "", string icon = "bi-check-circle-fill")
                => m.Observations.Add(new ItrDetailsViewModel.ItrObservation { Text = text, CssClass = css, Icon = icon });

            if (m.FiledOnTime)
                Add($"{m.FormType} for AY {m.AssessmentYear} was filed on time under section {m.FilingSection}.");
            else
                Add($"The return for AY {m.AssessmentYear} was filed late — belated filing limits loss carry-forward and signals weak compliance discipline.",
                    "obs-risk", "bi-exclamation-triangle-fill");

            if (m.EVerified && m.ReturnProcessed)
                Add("Return is e-verified and processed by CPC, so the declared figures are on record with the department.");
            else if (!m.EVerified)
                Add("Return is not e-verified. Until it is, the department treats it as not filed.",
                    "obs-danger", "bi-x-octagon-fill");

            if (m.HasTaxDemand)
                Add($"A demand of {Money(m.RefundOrDemandAmount)} is outstanding — self-assessment fell short of the final liability.",
                    "obs-danger", "bi-x-octagon-fill");
            else if (m.TaxPaidRatio >= 1)
                Add("Tax liability is fully discharged with no outstanding demand.");

            if (m.Interest234B + m.Interest234C > 0)
                Add($"Interest of {Money(m.Interest234B + m.Interest234C)} under sections 234B/234C indicates advance tax was short-paid or paid late — a cash-planning signal rather than evasion.",
                    "obs-observation", "bi-info-circle-fill");

            if (m.AuditApplicable && !m.AuditCompleted)
                Add($"Tax audit under {m.AuditSection} is applicable but Form 3CA/3CB has not been filed.",
                    "obs-danger", "bi-x-octagon-fill");
            else if (m.AuditCompleted)
                Add($"Tax audit completed by {m.AuditorName} (UDIN {m.Udin}), giving the reported financials third-party assurance.");

            if (m.HasGstReconciliation)
            {
                if (m.ReconciliationWithinTolerance)
                    Add($"GST and ITR turnover agree within {Math.Abs(m.VariancePercent):0.##}% — the two filings tell the same revenue story.");
                else
                    Add($"GST turnover differs from ITR-declared turnover by {Math.Abs(m.VariancePercent):0.##}% ({Money(Math.Abs(m.VarianceAmount))}). Worth reconciling before lending.",
                        "obs-risk", "bi-exclamation-triangle-fill");
            }

            if (m.HasFinancials && m.NetMarginPct.HasValue)
            {
                if (m.NetMarginPct >= 5)
                    Add($"Net margin of {m.NetMarginPct:0.##}% on turnover of {Money(m.RevenueFromOperations)} is healthy for an MSME.");
                else if (m.NetMarginPct >= 0)
                    Add($"Net margin of {m.NetMarginPct:0.##}% is thin — little cushion for a rate rise or a lost customer.",
                        "obs-observation", "bi-info-circle-fill");
                else
                    Add($"The business reported a loss for {m.FinancialYear}.", "obs-danger", "bi-x-octagon-fill");
            }

            if (m.DebtToEquity.HasValue && m.DebtToEquity > 3)
                Add($"Debt-to-equity of {m.DebtToEquity:0.00}x is high — outside liabilities are more than three times partners' capital.",
                    "obs-risk", "bi-exclamation-triangle-fill");

            if (m.HasPresumptive && m.CashTurnoverSharePct > 20)
                Add($"{m.CashTurnoverSharePct:0.#}% of turnover is in cash. Cash-heavy receipts are harder to verify against bank statements.",
                    "obs-observation", "bi-info-circle-fill");

            if (m.IsPresumptive)
                Add($"Presumptive return under section {m.SchemeSection} — income is declared at a flat rate, so no audited books support these figures.",
                    "obs-recommendation", "bi-lightbulb-fill");
        }

        private static void BuildVerificationMatrix(ItrDetailsViewModel m)
        {
            void Add(string label, bool pass)
                => m.VerificationItems.Add(new ItrDetailsViewModel.ItrVerifItem { Label = label, Pass = pass });

            Add("PAN present on return", m.Pan != "-");
            Add("GSTIN linked", m.Gstin != "-");
            Add("Filed by due date", m.FiledOnTime);
            Add("E-verified", m.EVerified);
            Add("Processed by CPC", m.ReturnProcessed);
            Add("No outstanding demand", !m.HasTaxDemand);
            Add("Tax fully discharged", m.TaxPaidRatio >= 1);
            Add("Audit obligation met", !m.AuditApplicable || m.AuditCompleted);
            if (m.HasGstReconciliation) Add("GST turnover reconciles", m.ReconciliationWithinTolerance);
            Add("Refund bank account on record", m.BankAccounts.Any(b => b.IsRefundAccount));
        }

        // ─────────────────────────── helpers ───────────────────────────

        private static string? Str(JObject? o, string name)
        {
            var t = o?[name];
            if (t == null || t.Type == JTokenType.Null) return null;
            var s = t.ToString().Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static double? Num(JObject? o, string name)
        {
            var t = o?[name];
            if (t == null || t.Type == JTokenType.Null) return null;
            return double.TryParse(t.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        private static bool Bool(JObject? o, string name) => o?[name]?.Value<bool?>() == true;

        private static long L(double? v) => (long)Math.Round(v ?? 0);

        private static DateTime? ParseDate(string? v)
            => DateTime.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d : null;

        private static string DateText(string? v)
        {
            var d = ParseDate(v);
            return d.HasValue ? d.Value.ToString("dd MMM yyyy") : "-";
        }

        private static int? AssessmentYearStart(string? assessmentYear)
            => int.TryParse(assessmentYear?.Split('-').FirstOrDefault(), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var y) && y > 1900 ? y : null;

        /// <summary>"PARTNERSHIP_FIRM" -> "Partnership Firm".</summary>
        private static string Pretty(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "-";
            var words = raw.Replace('_', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length <= 3 && w.All(char.IsUpper)
                    ? w                                     // keep DSC, EVC, NIL, LLP as-is
                    : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant());
            return string.Join(' ', words);
        }

        private static string Initials(string name)
        {
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            return parts.Length == 1
                ? parts[0][..1].ToUpperInvariant()
                : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
        }

        /// <summary>Indian short form — the same Cr / L scale the AA and MCA pages use.</summary>
        internal static string Money(long v)
        {
            var abs = Math.Abs(v);
            if (abs >= 10000000) return $"₹{v / 10000000m:0.##} Cr";
            if (abs >= 100000) return $"₹{v / 100000m:0.##} L";
            return $"₹{v:N0}";
        }
    }
}
