namespace FinRiskLensAI.Core.Models.Scoring
{
    /// <summary>
    /// All engineered features for one MSME, extracted from the raw source payloads
    /// (Udyam, GST, ITR, AA). Availability flags drive the missing-dimension weight
    /// redistribution described in Doc/03_SCORING_ENGINE.md.
    /// </summary>
    public class MsmeFeatureSet
    {
        // ── Source availability
        public bool HasUdyam { get; set; }
        public bool HasGst { get; set; }
        public bool HasItr { get; set; }
        public bool HasAa { get; set; }
        public bool HasEpfo { get; set; }     // data source arrives later — always false for now
        public bool HasBureau { get; set; }   // NTC borrowers have no bureau record

        // ── Udyam (identity / stability)
        public double BusinessVintageMonths { get; set; }
        public string EnterpriseType { get; set; } = string.Empty;   // Micro / Small / Medium
        public string MajorActivity { get; set; } = string.Empty;
        public int PlantLocationCount { get; set; }
        public int NicCodeCount { get; set; }
        /// <summary>Leading 2 digits of the primary NIC code (sector classification key).</summary>
        public string? SectorNic2 { get; set; }
        public string SectorName { get; set; } = string.Empty;
        /// <summary>Sector risk weight 0..1 (higher = more favourable industry outlook).</summary>
        public double SectorRiskWeight { get; set; } = 0.65;

        // ── GST (revenue + compliance)
        /// <summary>Monthly outward taxable turnover keyed by return period (yyyyMM sortable).</summary>
        public SortedDictionary<string, double> GstMonthlyTurnover { get; } = new();
        public double GstTurnoverTrendSlope { get; set; }        // normalized slope, -1..1
        public double GstFilingRegularity { get; set; }          // periods filed / periods expected, 0..1
        public double GstB2bShare { get; set; }                  // B2B taxable value / total, 0..1
        public int GstCounterpartyCount { get; set; }
        public bool GstRegistrationActive { get; set; }
        public double GstAnnualisedTurnover { get; set; }
        /// <summary>Credit/debit note value (GSTR-1 CDNR) relative to turnover — high = heavy revenue reversals.</summary>
        public double GstCreditNoteRatio { get; set; }
        /// <summary>GSTR-2A inward (purchase) value relative to outward turnover — sanity band for a real trading business.</summary>
        public double GstPurchaseToSalesRatio { get; set; }
        /// <summary>Distinct HSN/SAC codes sold (GSTR-1 HSN summary) — product/service mix diversity.</summary>
        public int GstHsnProductCount { get; set; }
        public double GstMonthlyAvgPurchases { get; set; }

        // ── GST deep-dive
        /// <summary>GSTR-1 vs GSTR-3B declared-sales agreement, 0..1 (1 = identical).</summary>
        public double GstR1Vs3bConsistency { get; set; }
        public bool GstHasTaxPaymentData { get; set; }
        public double GstCashTaxShare { get; set; }              // cash / (cash + ITC), 0..1
        public double GstItcMonthlyAvg { get; set; }
        public double GstTopCustomerShare { get; set; }          // 0..1 of B2B sales
        public double GstTopVendorShare { get; set; }            // 0..1 of purchases
        public Dictionary<string, double> GstTopCustomers { get; } = new();
        public Dictionary<string, double> GstTopVendors { get; } = new();

        // ── GSTR-2B (auto-drafted ITC statement)
        public bool GstHas2bData { get; set; }
        /// <summary>Monthly average ITC available per GSTR-2B (all heads).</summary>
        public double GstItcAvailableMonthly { get; set; }
        /// <summary>ITC claimed in GSTR-3B vs available per GSTR-2B — >1 means over-claiming (red flag).</summary>
        public double GstItcClaimVsAvailable { get; set; }
        /// <summary>Share of ITC marked unavailable in 2B, 0..1 — vendor-quality signal.</summary>
        public double GstItcUnavailableShare { get; set; }
        /// <summary>Share of B2B suppliers whose returns show a filing date in 2B, 0..1.</summary>
        public double GstSupplierFilingRate { get; set; }

        // ── ITR (income + compliance)
        /// <summary>Gross total income keyed by assessment year label (e.g. "2024-2025").</summary>
        public SortedDictionary<string, double> ItrYearlyIncome { get; } = new();
        public double ItrIncomeTrendSlope { get; set; }          // normalized slope, -1..1
        public double ItrFilingTimeliness { get; set; }          // on-time filings / total, 0..1
        public int ItrYearsFiled { get; set; }
        public double ItrLatestIncome { get; set; }

        // ── ITR financial ratios (business filers with books — ITR-3/5/6 only)
        public string? ItrFinancialsYear { get; set; }
        public double ItrBusinessTurnover { get; set; }
        public double? ItrEbitdaMargin { get; set; }
        public double? ItrNetProfitMargin { get; set; }
        public double? ItrDebtorDays { get; set; }
        public double? ItrAssetTurnover { get; set; }

        // ── ITR filing quality (single-year vendor response)
        /// <summary>"ITR-4 (SUGAM)" for presumptive filers, "ITR-5" for books-of-account filers.</summary>
        public string? ItrFormType { get; set; }
        public string? ItrAssessmentYear { get; set; }
        /// <summary>139(1) = filed by the due date, 139(4) = belated, 139(5) = revised.</summary>
        public string? ItrFilingSection { get; set; }
        public bool ItrEVerified { get; set; }
        public bool ItrReturnProcessed { get; set; }
        /// <summary>Tax audit required under 44AB (turnover over the threshold).</summary>
        public bool ItrAuditApplicable { get; set; }
        /// <summary>Audit required <i>and</i> Form 3CA/3CB actually filed.</summary>
        public bool ItrAuditCompleted { get; set; }
        /// <summary>A demand was raised on assessment — taxes short-paid.</summary>
        public bool ItrHasTaxDemand { get; set; }
        /// <summary>Taxes paid ÷ total tax and interest, 0..1. 1 = fully discharged.</summary>
        public double ItrTaxPaidRatio { get; set; }
        /// <summary>Interest u/s 234A — charged only when the return itself was late.</summary>
        public double ItrLateFilingInterest { get; set; }
        /// <summary>Interest u/s 234B + 234C — advance-tax shortfall, a cash-planning signal.</summary>
        public double ItrAdvanceTaxInterest { get; set; }

        // ── ITR financials (from the P&L / balance-sheet summary)
        /// <summary>Profit before tax ÷ turnover. The vendor response has no PBIDTA line,
        /// so this is the closest honest operating-margin proxy — see also
        /// <see cref="ItrEbitdaMargin"/>, which stays null on this response shape.</summary>
        public double? ItrPbtMargin { get; set; }
        public double ItrTotalAssets { get; set; }
        /// <summary>Partners'/members' capital — the net-worth line the return reports.</summary>
        public double ItrNetWorth { get; set; }
        /// <summary>Outside liabilities ÷ capital. Null when the return reports no capital.</summary>
        public double? ItrDebtToEquity { get; set; }

        /// <summary>True for 44AD/44ADA presumptive returns (ITR-4), which carry no real books.</summary>
        public bool ItrIsPresumptive { get; set; }
        /// <summary>Cash turnover ÷ total turnover for presumptive filers, 0..1.</summary>
        public double ItrCashTurnoverShare { get; set; }

        /// <summary>Vendor-computed GST-vs-ITR turnover variance, in percent. Null when absent.</summary>
        public double? ItrGstTurnoverVariancePct { get; set; }

        // ── AA bank statements (cash flow + transaction quality + debt)
        public SortedDictionary<string, double> AaMonthlyCredits { get; } = new();
        public SortedDictionary<string, double> AaMonthlyDebits { get; } = new();
        public double AaCurrentBalanceTotal { get; set; }
        public double AaAvgMonthlyCredit { get; set; }
        public double AaInflowVolatility { get; set; }           // coefficient of variation of monthly credits
        public double AaDaysCashOnHand { get; set; }
        public int AaBounceCount { get; set; }
        public double AaUpiTxnShare { get; set; }                // UPI txns / all txns, 0..1
        public int AaUpiCounterpartyCount { get; set; }
        public double AaRepeatPayerRatio { get; set; }           // repeat credit payers / credit txns, 0..1
        public double AaEmiMonthlyOutflow { get; set; }
        public double AaEmiToInflowRatio { get; set; }
        public int AaTransactionCount { get; set; }
        public int AaMonthsCovered { get; set; }
        public double AaCashflowTrendSlope { get; set; }         // filled by the SSA/trend analyzer, -1..1

        // ── AA deep-dive (bank statement behaviour)
        public SortedDictionary<string, double> AaMonthlyAvgBalance { get; } = new();
        public double AaAvgMonthlyBalance { get; set; }          // AMB across the window
        public double AaPeakBalance { get; set; }
        public double AaCashDepositsMonthly { get; set; }
        public double AaSalaryCreditsMonthly { get; set; }
        public double AaCustomerReceiptsMonthly { get; set; }
        public double AaSupplierPaymentsMonthly { get; set; }
        public int AaChequeReturnCount { get; set; }
        public int AaEcsNachReturnCount { get; set; }
        public double AaOdLimitTotal { get; set; }
        public int AaOverdrawnTxnCount { get; set; }             // balance snapshots below zero
        public int AaMinBalanceBreachCount { get; set; }         // balance snapshots below threshold
        public Dictionary<string, double> AaModeSplitAmount { get; } = new();

        // ── Cross-source income signals (for the fraud/anomaly check)
        public double GstMonthlyAvgTurnover { get; set; }
        public double ItrMonthlyAvgIncome { get; set; }
        public double AaMonthlyAvgBankCredit { get; set; }
    }
}
