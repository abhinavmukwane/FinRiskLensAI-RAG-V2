using System;
using System.Collections.Generic;

namespace FinRiskLensAI.Core.Models.AccountAggregator
{
    /// <summary>
    /// One normalised bank-statement row, independent of the source envelope.
    /// The AA feed arrives in two shapes (Finvu fiObjects/Transactions and the
    /// ledger row[] format); both are flattened into this record so every
    /// downstream calculation has a single contract.
    /// </summary>
    public class AaTransaction
    {
        public DateTime Date { get; set; }
        public DateTime? ValueDate { get; set; }
        public string TxnNo { get; set; } = "";
        public string Narration { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public decimal? Balance { get; set; }
        public string Mode { get; set; } = "";
        public string ChequeNo { get; set; } = "";
        /// <summary>Ledger feeds mark returned/unpassed rows; Finvu has no equivalent.</summary>
        public bool IsUnpassed { get; set; }
        public string AccountRef { get; set; } = "";

        // ── Assigned by the classifier ───────────────────────────────────
        public string Category { get; set; } = AaCategory.Unknown;
        /// <summary>High | Medium | Low — how far the classification is evidence-backed.</summary>
        public string Confidence { get; set; } = "Low";
        /// <summary>Set only when a rule fires; null means no flag.</summary>
        public string? RiskFlag { get; set; }

        public bool IsCredit => Credit > 0m;
        public decimal Amount => Credit > 0m ? Credit : Debit;
        public string Direction => IsCredit ? "CREDIT" : "DEBIT";
    }

    /// <summary>Category keys. Kept as constants so view, service and AI payload agree.</summary>
    public static class AaCategory
    {
        // Inflow
        public const string BusinessReceipt = "Potential Business Receipt";
        public const string TradeReceipt = "Customer/Trade Receipt";
        public const string LoanFinance = "Loan / Finance Related";
        public const string Investment = "Investment";
        public const string Refund = "Refund";
        public const string CashDeposit = "Cash Deposit";
        public const string InterestIncome = "Interest";

        // Outflow
        public const string BusinessExpense = "Business Expense";
        public const string CashWithdrawal = "Cash Withdrawal";
        public const string LoanEmi = "Loan / EMI";
        public const string NachEcs = "NACH/ECS";
        public const string GstTax = "GST/Tax";
        public const string Rent = "Rent";
        public const string Utility = "Utility";
        public const string BankCharges = "Bank Charges";
        public const string ChequePayment = "Cheque Payment";
        public const string InterestPaid = "Interest";

        // Both directions
        public const string SelfTransfer = "Own Account / Self Transfer";
        public const string Salary = "Salary";
        public const string Other = "Other";
        public const string Unknown = "Unknown";
    }

    /// <summary>Linked account as reported by the FIP — shown on the AA Detail page.</summary>
    public class AaAccountInfo
    {
        public string MaskedAccountNumber { get; set; } = "-";
        public string FipName { get; set; } = "-";
        public string Type { get; set; } = "-";
        public decimal? CurrentBalance { get; set; }
        public string Status { get; set; } = "-";
        public string Branch { get; set; } = "-";
        public string IfscCode { get; set; } = "-";
        public string OpeningDate { get; set; } = "-";
        public int TransactionCount { get; set; }
    }

    public class AaCategorySummary
    {
        public string Category { get; set; } = "";
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; }
        /// <summary>Whether the bucket is treated as operating business inflow.</summary>
        public bool CountsAsBusinessInflow { get; set; }
    }

    public class AaMonthlyRow
    {
        public string Month { get; set; } = "";          // yyyy-MM
        public string MonthLabel { get; set; } = "";     // MMM yyyy
        public decimal Credits { get; set; }
        public decimal BusinessCredits { get; set; }
        public decimal SelfTransfers { get; set; }
        public decimal PassThrough { get; set; }
        public decimal Debits { get; set; }
        public decimal CashWithdrawals { get; set; }
        public decimal Obligations { get; set; }
        public decimal? ClosingBalance { get; set; }
        public decimal? AverageBalance { get; set; }
        public decimal NetCashFlow => Credits - Debits;
        public int TxnCount { get; set; }
        public string? RiskFlag { get; set; }
    }

    /// <summary>A credit followed by a similar-value debit inside the lookahead window.</summary>
    public class AaPassThroughEvent
    {
        public DateTime CreditDate { get; set; }
        public decimal CreditAmount { get; set; }
        public string CreditNarration { get; set; } = "";
        public DateTime DebitDate { get; set; }
        public decimal DebitAmount { get; set; }
        public string DebitNarration { get; set; } = "";
        public string DebitMode { get; set; } = "";
        public int DaysGap { get; set; }
        public decimal AmountDifference { get; set; }
        /// <summary>High | Moderate | Low — tighter amount/gap ⇒ higher.</summary>
        public string Confidence { get; set; } = "Low";
        public string RiskLevel { get; set; } = "Low";
    }

    public class AaRiskFlag
    {
        /// <summary>High | Medium | Positive.</summary>
        public string Severity { get; set; } = "Medium";
        public string Title { get; set; } = "";
        public string Evidence { get; set; } = "";
        public string CreditRelevance { get; set; } = "";
    }

    public class AaObligation
    {
        public string Label { get; set; } = "";
        public string Category { get; set; } = "";
        public int Occurrences { get; set; }
        public decimal TypicalAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public int AverageIntervalDays { get; set; }
        public string Confidence { get; set; } = "Low";
        public List<AaTransaction> Transactions { get; set; } = new();
    }

    public class AaLiquidityMetric
    {
        public string Metric { get; set; } = "";
        public string Formula { get; set; } = "";
        public string Value { get; set; } = "";
        public string RiskCategory { get; set; } = "";
        public string Interpretation { get; set; } = "";
    }

    public class AaScoreComponent
    {
        public string Name { get; set; } = "";
        public int Score { get; set; }        // 0-100
        public int Weight { get; set; }       // percent
        public string Reason { get; set; } = "";
    }

    /// <summary>Structured AI output. Validated before it reaches the view.</summary>
    public class AaAiInsights
    {
        public bool Available { get; set; }
        public string? Error { get; set; }
        public string OverallAssessment { get; set; } = "";
        public string RiskLevel { get; set; } = "";
        public List<string> KeyStrengths { get; set; } = new();
        public List<string> KeyConcerns { get; set; } = new();
        public string CashFlowAssessment { get; set; } = "";
        public string InflowAssessment { get; set; } = "";
        public string OutflowAssessment { get; set; } = "";
        public string PassThroughAssessment { get; set; } = "";
        public string ObligationAssessment { get; set; } = "";
        public string BalanceAssessment { get; set; } = "";
        public string BankingDisciplineAssessment { get; set; } = "";
        public string CreditAnalystSummary { get; set; } = "";
        public List<string> RecommendedVerification { get; set; } = new();
    }

    /// <summary>
    /// The complete deterministic analysis. Everything here is computed from the
    /// statement itself — the AI layer only adds narrative on top and never
    /// feeds back into these numbers.
    /// </summary>
    public class AaAnalysisResult
    {
        public bool HasData { get; set; }
        public string? LoadError { get; set; }
        public string? Uan { get; set; }

        /// <summary>Linked accounts reported by the FIP(s).</summary>
        public List<AaAccountInfo> Accounts { get; set; } = new();

        // ── Section 1: executive summary ─────────────────────────────────
        public DateTime? PeriodFrom { get; set; }
        public DateTime? PeriodTo { get; set; }
        public string AnalysisPeriod { get; set; } = "-";
        public int MonthsCovered { get; set; }
        public int TransactionCount { get; set; }
        public int CreditCount { get; set; }
        public int DebitCount { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal NetCashFlow => TotalCredits - TotalDebits;
        public decimal AvgMonthlyCredit { get; set; }
        public decimal AvgMonthlyDebit { get; set; }
        public decimal AvgBalance { get; set; }
        public decimal MedianBalance { get; set; }
        public decimal MinBalance { get; set; }
        public decimal MaxBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
        public bool BalanceAvailable { get; set; }

        public decimal PotentialBusinessInflow { get; set; }
        public decimal PotentialNonBusinessInflow { get; set; }
        public decimal PotentialSelfTransferCredits { get; set; }
        public decimal InterestIncome { get; set; }
        public decimal OtherUnknownInflow { get; set; }
        public decimal PotentialPassThroughAmount { get; set; }

        public int HealthScore { get; set; }
        public string RiskGrade { get; set; } = "-";
        public string ScoreExplanation { get; set; } = "";
        public List<AaScoreComponent> ScoreComponents { get; set; } = new();

        // ── Sections 2-4 ─────────────────────────────────────────────────
        public decimal CreditToDebitRatio { get; set; }
        public List<AaCategorySummary> InflowCategories { get; set; } = new();
        public List<AaCategorySummary> OutflowCategories { get; set; } = new();

        // ── Section 6: cash ──────────────────────────────────────────────
        public decimal TotalCashDeposits { get; set; }
        public decimal TotalCashWithdrawals { get; set; }
        public int CashDepositCount { get; set; }
        public int CashWithdrawalCount { get; set; }
        public decimal CashWithdrawalRatio { get; set; }
        public decimal CashDepositRatio { get; set; }
        public decimal AvgCashWithdrawal { get; set; }
        public decimal LargestCashWithdrawal { get; set; }
        public decimal LargestCashDeposit { get; set; }
        public string? CashPatternNote { get; set; }

        // ── Section 7: self transfers ────────────────────────────────────
        public int SelfTransferCount { get; set; }
        public decimal SelfTransferAmount { get; set; }
        public decimal SelfTransferPctOfCredits { get; set; }
        public decimal SelfTransferPctOfDebits { get; set; }

        // ── Section 8: pass-through ──────────────────────────────────────
        public List<AaPassThroughEvent> PassThroughEvents { get; set; } = new();
        public decimal PassThroughPctOfCredits { get; set; }
        public decimal PassThroughPctOfBusinessInflow { get; set; }
        public string PassThroughRiskLevel { get; set; } = "Low";

        // ── Sections 9-10 ────────────────────────────────────────────────
        public int LowBalanceEvents { get; set; }
        public decimal LowBalanceThreshold { get; set; }
        public int NegativeBalanceEvents { get; set; }
        public List<AaLiquidityMetric> LiquidityMetrics { get; set; } = new();

        // ── Section 11: obligations ──────────────────────────────────────
        public List<AaObligation> Obligations { get; set; } = new();
        public decimal EstimatedMonthlyObligation { get; set; }
        public decimal TotalObligationAmount { get; set; }
        public decimal ObligationToBusinessInflowRatio { get; set; }

        // ── Section 12: banking discipline ───────────────────────────────
        public List<AaTransaction> DisciplineEvents { get; set; } = new();
        public decimal BankChargesTotal { get; set; }
        public string DisciplineNote { get; set; } = "";

        // ── Section 13: frequency ────────────────────────────────────────
        public decimal TxnPerMonth { get; set; }
        public decimal AvgTxnSize { get; set; }
        public decimal MedianTxnSize { get; set; }
        public decimal LargestTxn { get; set; }
        public decimal SmallestTxn { get; set; }
        public string ActivityPattern { get; set; } = "-";

        // ── Section 14: concentration ────────────────────────────────────
        public decimal Top5CreditPct { get; set; }
        public decimal Top10CreditPct { get; set; }
        public decimal Top5DebitPct { get; set; }
        public List<AaTransaction> TopCredits { get; set; } = new();
        public List<AaTransaction> TopDebits { get; set; } = new();
        public List<string> RepeatedValueNotes { get; set; } = new();

        // ── Sections 15-17 ───────────────────────────────────────────────
        public List<AaMonthlyRow> Monthly { get; set; } = new();
        public List<AaRiskFlag> RiskFlags { get; set; } = new();
        public List<string> CreditAnalystView { get; set; } = new();
        public decimal EstimatedSustainableInflow { get; set; }
        public List<string> SustainableMethodology { get; set; } = new();

        // ── Transaction drill-down (section 5) ───────────────────────────
        public List<AaTransaction> Transactions { get; set; } = new();

        // ── Section 18 ───────────────────────────────────────────────────
        public AaAiInsights? Ai { get; set; }
    }
}
