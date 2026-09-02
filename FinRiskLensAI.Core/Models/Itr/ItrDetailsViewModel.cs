namespace FinRiskLensAI.Core.Models.Itr
{
    /// <summary>
    /// ViewModel for /Itr/ItrDetails — fully computed by ItrController.BuildAnalysis
    /// from the ITR vendor response stored as itr.json in the MSME's blob folder.
    /// The Razor view only renders these values; it holds no business logic, same
    /// contract as <c>UdyamDetailsViewModel</c> and <c>McaDetailsViewModel</c>.
    /// <para>
    /// The response carries a single assessment year in one of two shapes —
    /// ITR-4 (SUGAM) presumptive for proprietorships, ITR-5 books-of-account for
    /// partnerships / LLPs / companies. Fields the active shape does not carry
    /// stay null so the view can hide the panel rather than print a fake zero.
    /// </para>
    /// </summary>
    public class ItrDetailsViewModel
    {
        public bool HasData { get; set; }
        public string? LoadError { get; set; }
        public string? Uan { get; set; }

        // ── Entity ────────────────────────────────────────────────────────
        public string EntityName { get; set; } = "-";
        public string ConstitutionType { get; set; } = "-";
        public string Pan { get; set; } = "-";
        public string PanMasked { get; set; } = "-";
        public string Gstin { get; set; } = "-";
        public string GstinMasked { get; set; } = "-";
        public string? Llpin { get; set; }
        public string RegisteredAddress { get; set; } = "-";
        public string Email { get; set; } = "-";
        public string Mobile { get; set; } = "-";
        public string DateOfFormationText { get; set; } = "-";

        // ── Filing ────────────────────────────────────────────────────────
        public string FormType { get; set; } = "-";
        public string AssessmentYear { get; set; } = "-";
        public string FinancialYear { get; set; } = "-";
        public string FilingType { get; set; } = "-";
        public string FilingSection { get; set; } = "-";
        public string AcknowledgementNumber { get; set; } = "-";
        public string FilingDateText { get; set; } = "-";
        public string FilingMode { get; set; } = "-";
        public string EVerificationStatus { get; set; } = "-";
        public string EVerificationDateText { get; set; } = "-";
        public string ReturnStatus { get; set; } = "-";
        public bool IsPresumptive { get; set; }

        /// <summary>Filed on or before the statutory due date for the assessment year.</summary>
        public bool FiledOnTime { get; set; }
        public string DueDateText { get; set; } = "-";
        public bool EVerified { get; set; }
        public bool ReturnProcessed { get; set; }

        // ── Income ────────────────────────────────────────────────────────
        public long BusinessIncome { get; set; }
        public long HousePropertyIncome { get; set; }
        public long CapitalGains { get; set; }
        public long OtherSourcesIncome { get; set; }
        public long GrossTotalIncome { get; set; }
        public long TotalDeductions { get; set; }
        public long TotalIncome { get; set; }
        public List<ItrAmountRow> DeductionRows { get; set; } = new();

        // ── Tax computation ───────────────────────────────────────────────
        public string TaxRateText { get; set; } = "-";
        public long TaxOnTotalIncome { get; set; }
        public long Surcharge { get; set; }
        public long Cess { get; set; }
        public long TotalTaxLiability { get; set; }
        public long Interest234A { get; set; }
        public long Interest234B { get; set; }
        public long Interest234C { get; set; }
        public long TotalTaxAndInterest { get; set; }
        public long AdvanceTax { get; set; }
        public long Tds { get; set; }
        public long Tcs { get; set; }
        public long SelfAssessmentTax { get; set; }
        public long TotalTaxesPaid { get; set; }
        public string RefundOrDemandType { get; set; } = "NIL";
        public long RefundOrDemandAmount { get; set; }
        public bool HasTaxDemand { get; set; }
        /// <summary>Taxes paid ÷ total tax and interest, 0..1.</summary>
        public double TaxPaidRatio { get; set; }

        // ── Presumptive (ITR-4 only) ──────────────────────────────────────
        public bool HasPresumptive { get; set; }
        public string SchemeSection { get; set; } = "-";
        public string NatureOfBusiness { get; set; } = "-";
        public long PresumptiveTurnover { get; set; }
        public long TurnoverBanking { get; set; }
        public long TurnoverCash { get; set; }
        public double CashTurnoverSharePct { get; set; }
        public long PresumptiveIncomeDeclared { get; set; }
        public string EffectiveRateText { get; set; } = "-";

        // ── P&L / balance sheet (ITR-5 only) ──────────────────────────────
        public bool HasFinancials { get; set; }
        public string PeriodText { get; set; } = "-";
        public long RevenueFromOperations { get; set; }
        public long OtherIncome { get; set; }
        public long TotalExpenses { get; set; }
        public long NetProfitBeforeTax { get; set; }
        public long NetProfitAfterTax { get; set; }

        public bool HasBalanceSheet { get; set; }
        public string BalanceSheetDateText { get; set; } = "-";
        public long TotalPartnersCapital { get; set; }
        public long TotalLiabilities { get; set; }
        public long TotalAssets { get; set; }

        // ── Derived ratios. Null when the return does not carry the source figure. ──
        public double? PbtMarginPct { get; set; }
        public double? NetMarginPct { get; set; }
        public double? AssetTurnover { get; set; }
        public double? DebtToEquity { get; set; }

        // ── Partners (ITR-5) ──────────────────────────────────────────────
        public List<ItrPartnerItem> Partners { get; set; } = new();
        public long TotalRemuneration { get; set; }
        public long TotalInterestOnCapital { get; set; }
        public long Allowable40b { get; set; }
        public long Disallowed40b { get; set; }

        // ── GST reconciliation ────────────────────────────────────────────
        public bool HasGstReconciliation { get; set; }
        public long GstReportedTurnover { get; set; }
        public long ItrDeclaredTurnover { get; set; }
        public long VarianceAmount { get; set; }
        public double VariancePercent { get; set; }
        public string ReconciliationStatus { get; set; } = "-";
        public bool ReconciliationWithinTolerance { get; set; }

        // ── Audit ─────────────────────────────────────────────────────────
        public bool AuditApplicable { get; set; }
        public bool AuditCompleted { get; set; }
        public string AuditSection { get; set; } = "-";
        public string AuditorName { get; set; } = "-";
        public string AuditorMembership { get; set; } = "-";
        public string Udin { get; set; } = "-";
        public string AuditFilingDateText { get; set; } = "-";

        // ── Bank accounts + verification ──────────────────────────────────
        public List<ItrBankItem> BankAccounts { get; set; } = new();
        public string VerifiedByName { get; set; } = "-";
        public string VerifierDesignation { get; set; } = "-";
        public string VerificationDateText { get; set; } = "-";
        public string VerificationPlace { get; set; } = "-";

        // ── Filing & tax health score (0–100, higher is healthier) ─────────
        public int Score { get; set; }
        public string RiskLabel { get; set; } = "-";
        public string RiskCss { get; set; } = "risk-medium";
        public List<ItrScoreComponent> ScoreComponents { get; set; } = new();
        public List<ItrObservation> Observations { get; set; } = new();
        public List<ItrVerifItem> VerificationItems { get; set; } = new();

        public class ItrAmountRow
        {
            public string Label { get; set; } = "";
            public long Amount { get; set; }
            public string AmountText { get; set; } = "₹0";
        }

        public class ItrPartnerItem
        {
            public string Name { get; set; } = "-";
            public string Initials { get; set; } = "?";
            public string Pan { get; set; } = "-";
            public string PanMasked { get; set; } = "-";
            public double SharePercent { get; set; }
            public long CapitalBalance { get; set; }
            public string CapitalText { get; set; } = "₹0";
            public bool IsWorkingPartner { get; set; }
        }

        public class ItrBankItem
        {
            public string BankName { get; set; } = "-";
            public string AccountMasked { get; set; } = "-";
            public string Ifsc { get; set; } = "-";
            public string AccountType { get; set; } = "-";
            public bool IsRefundAccount { get; set; }
        }

        public class ItrScoreComponent
        {
            public string Name { get; set; } = "";
            /// <summary>Points earned out of <see cref="MaxPoints"/>.</summary>
            public int Points { get; set; }
            public int MaxPoints { get; set; }
            public int Percent { get; set; }
            public string Reason { get; set; } = "";
            /// <summary>fill-good | fill-warn | fill-bad.</summary>
            public string FillCss { get; set; } = "fill-good";
        }

        public class ItrObservation
        {
            public string Text { get; set; } = "";
            /// <summary>"" (success) | obs-observation | obs-risk | obs-recommendation | obs-danger.</summary>
            public string CssClass { get; set; } = "";
            public string Icon { get; set; } = "bi-check-circle-fill";
        }

        public class ItrVerifItem
        {
            public string Label { get; set; } = "";
            public bool Pass { get; set; }
        }
    }
}
