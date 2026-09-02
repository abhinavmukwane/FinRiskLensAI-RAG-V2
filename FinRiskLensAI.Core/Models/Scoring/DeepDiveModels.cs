namespace FinRiskLensAI.Core.Models.Scoring
{
    /// <summary>Bank-statement behaviour analysis from the AA data (underwriting deep-dive).</summary>
    public class BankStatementAnalysis
    {
        public double AverageMonthlyBalance { get; set; }
        public double PeakBalance { get; set; }
        public double AverageMonthlyCredit { get; set; }
        public double CashDepositsMonthly { get; set; }
        public double SalaryCreditsMonthly { get; set; }
        public double CustomerReceiptsMonthly { get; set; }
        public double SupplierPaymentsMonthly { get; set; }
        public int BounceCount { get; set; }
        public int ChequeReturnCount { get; set; }
        public int EcsNachReturnCount { get; set; }
        public double OdLimitTotal { get; set; }
        public int OverdrawnTxnCount { get; set; }
        public int MinBalanceBreachCount { get; set; }
        /// <summary>Average balance per month (yyyyMM → ₹) for the balance trend chart.</summary>
        public Dictionary<string, double> MonthlyAvgBalance { get; set; } = new();
        /// <summary>Transaction value split by mode (UPI/FT/IMPS/RTGS/CASH/…).</summary>
        public Dictionary<string, double> ModeSplitAmount { get; set; } = new();
        public Dictionary<string, double> MonthlyCredits { get; set; } = new();
        public Dictionary<string, double> MonthlyDebits { get; set; } = new();
    }

    /// <summary>GST behaviour deep-dive: consistency, tax discipline, ITC, concentration.</summary>
    public class GstDeepDive
    {
        /// <summary>GSTR-1 vs GSTR-3B declared-sales agreement, 0..1 (1 = identical).</summary>
        public double R1Vs3bConsistency { get; set; }
        public bool HasTaxPaymentData { get; set; }
        /// <summary>Share of tax settled in cash vs input tax credit, 0..1.</summary>
        public double CashTaxShare { get; set; }
        public double ItcMonthlyAvg { get; set; }
        /// <summary>Largest customer's share of B2B sales, 0..1 (concentration risk).</summary>
        public double TopCustomerShare { get; set; }
        public double TopVendorShare { get; set; }
        /// <summary>Top customers by B2B taxable value (masked GSTIN → share 0..1).</summary>
        public Dictionary<string, double> TopCustomers { get; set; } = new();
        public Dictionary<string, double> TopVendors { get; set; } = new();
        public Dictionary<string, double> MonthlySales { get; set; } = new();

        // ── GSTR-2B (auto-drafted ITC statement)
        public bool Has2bData { get; set; }
        /// <summary>Monthly average ITC available per GSTR-2B.</summary>
        public double ItcAvailableMonthly { get; set; }
        /// <summary>ITC claimed (3B) vs available (2B) — >1 = over-claiming, a compliance red flag.</summary>
        public double ItcClaimVsAvailable { get; set; }
        /// <summary>Share of ITC marked unavailable in 2B, 0..1.</summary>
        public double ItcUnavailableShare { get; set; }
        /// <summary>Share of B2B suppliers with a return filing date in 2B, 0..1.</summary>
        public double SupplierFilingRate { get; set; }
    }

    /// <summary>P&L / balance-sheet ratios from ITR (business filers only — null = not derivable).</summary>
    public class FinancialRatios
    {
        public bool HasFinancials { get; set; }
        public string? SourceYear { get; set; }
        public double BusinessTurnover { get; set; }
        /// <summary>PBIDTA / turnover. Null on the current ITR response, which carries
        /// no PBIDTA line — use <see cref="PbtMargin"/> for the operating signal.</summary>
        public double? EbitdaMargin { get; set; }
        public double? PbtMargin { get; set; }           // profit before tax / turnover
        public double? NetProfitMargin { get; set; }     // PAT / turnover
        public double? DebtorDays { get; set; }          // sundry debtors / turnover × 365
        public double? AssetTurnover { get; set; }       // turnover / total assets
        public double TotalAssets { get; set; }
        public double NetWorth { get; set; }             // partners'/members' capital
        public double? DebtToEquity { get; set; }        // outside liabilities / capital

        // ── Filing quality (single assessment year, from the ITR vendor response)
        public string? FormType { get; set; }
        public string? FilingSection { get; set; }
        public bool IsPresumptive { get; set; }
        public bool EVerified { get; set; }
        public bool ReturnProcessed { get; set; }
        public bool AuditApplicable { get; set; }
        public bool AuditCompleted { get; set; }
        public bool HasTaxDemand { get; set; }
        /// <summary>Absolute GST-vs-ITR declared-turnover variance, in percent.</summary>
        public double? GstTurnoverVariancePct { get; set; }
    }

    /// <summary>Sector risk classification from the Udyam NIC code.</summary>
    public class IndustryRiskInfo
    {
        public string SectorName { get; set; } = string.Empty;
        public string? Nic2Digit { get; set; }
        /// <summary>0..1 — higher is lower-risk (1 = most favourable sector outlook).</summary>
        public double RiskWeight { get; set; }
        public string Outlook { get; set; } = string.Empty;   // Favourable / Moderate / Cautious
    }
}
