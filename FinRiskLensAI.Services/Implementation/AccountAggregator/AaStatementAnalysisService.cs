using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Models.AccountAggregator;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Services.Implementation.AccountAggregator
{
    /// <summary>
    /// Bank-statement deep analysis for MSME credit assessment.
    ///
    /// Design notes:
    ///  • The statement is parsed, normalised and sorted ONCE; every section then
    ///    works off that single in-memory list.
    ///  • Pass-through detection is amount-indexed + binary-searched rather than a
    ///    full credit×debit sweep, so it stays near O(n log n) on large statements.
    ///  • Nothing here assumes total credits equal business turnover. Inflows are
    ///    split into business / non-business / self-transfer / interest / unknown,
    ///    and uncertain classifications are labelled "Potential".
    /// </summary>
    public class AaStatementAnalysisService : IAaStatementAnalysisService
    {
        // Pass-through matching windows and tolerance.
        private static readonly int[] PassThroughWindows = { 0, 1, 3, 7, 15 };
        private const decimal PassThroughPctTolerance = 0.02m;  // 2 %
        private const decimal PassThroughAbsTolerance = 100m;   // or ₹100, whichever is larger

        private static readonly string[] DateFormats =
        {
            "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:sszzz", "yyyy/MM/dd",
            "dd/MM/yyyy", "dd-MM-yyyy", "dd-MMM-yyyy", "dd MMM yyyy", "MM/dd/yyyy"
        };

        // ═════════════════════════════════════════════════════════════════
        //  Entry point
        // ═════════════════════════════════════════════════════════════════
        public AaAnalysisResult Analyze(string? aaJson)
        {
            var r = new AaAnalysisResult();

            if (string.IsNullOrWhiteSpace(aaJson))
            {
                r.LoadError = "No Account Aggregator statement is stored for this MSME yet.";
                return r;
            }

            JToken root;
            try
            {
                root = JToken.Parse(aaJson);
            }
            catch (JsonException)
            {
                r.LoadError = "The stored Account Aggregator response is not valid JSON and could not be analysed.";
                return r;
            }

            List<AaTransaction> txns;
            try
            {
                txns = Normalize(root, r);
            }
            catch (Exception)
            {
                r.LoadError = "The Account Aggregator statement could not be read — its structure was not recognised.";
                return r;
            }

            if (txns.Count == 0)
            {
                r.LoadError = "The Account Aggregator statement contains no readable transactions (empty ledger).";
                return r;
            }

            txns = txns.OrderBy(t => t.Date).ThenBy(t => t.TxnNo).ToList();
            foreach (var t in txns) Classify(t);

            r.HasData = true;
            r.Transactions = txns;

            BuildSummary(r, txns);
            BuildCategories(r, txns);
            BuildCash(r, txns);
            BuildSelfTransfers(r, txns);
            BuildPassThrough(r, txns);
            BuildBalance(r, txns);
            BuildObligations(r, txns);
            BuildDiscipline(r, txns);
            BuildFrequency(r, txns);
            BuildConcentration(r, txns);
            BuildMonthly(r, txns);
            BuildLiquidity(r);
            BuildSustainable(r);
            BuildScore(r);
            BuildRiskFlags(r);
            BuildAnalystView(r);

            return r;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PHASE 1 — normalisation (accepts both AA envelopes)
        // ═════════════════════════════════════════════════════════════════
        private static List<AaTransaction> Normalize(JToken root, AaAnalysisResult r)
        {
            var list = new List<AaTransaction>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // FIP name sits on the parent body[] node, not on the account itself.
            var fipNames = root.SelectTokens("$..body[*]").OfType<JObject>()
                .ToDictionary(b => b, b => Prop(b, "fipName", "fipId") ?? "-");

            // ── Shape A: Finvu FI-data — body[] → fiObjects[] → Transactions.Transaction[]
            foreach (var acct in root.SelectTokens("$..fiObjects[*]").OfType<JObject>())
            {
                var accRef = Prop(acct, "maskedAccNumber", "linkedAccRef") ?? "";
                var parentBody = acct.Ancestors().OfType<JObject>().FirstOrDefault(a => fipNames.ContainsKey(a));

                var before = list.Count;
                foreach (var t in acct.SelectTokens("Transactions.Transaction[*]").OfType<JObject>())
                {
                    var id = Prop(t, "txnId") ?? Guid.NewGuid().ToString();
                    if (!seen.Add("A:" + id)) continue;   // same txn can repeat across FIPs

                    var dateStr = Prop(t, "valueDate", "transactionTimestamp");
                    if (!TryDate(dateStr, out var date)) continue;

                    var amount = Dec(Prop(t, "amount"));
                    var isCredit = string.Equals(Prop(t, "type"), "CREDIT", StringComparison.OrdinalIgnoreCase);

                    list.Add(new AaTransaction
                    {
                        Date = date,
                        ValueDate = TryDate(Prop(t, "valueDate"), out var vd) ? vd : null,
                        TxnNo = Prop(t, "txnId") ?? "",
                        Narration = Prop(t, "narration") ?? "",
                        Description = Prop(t, "reference") ?? "",
                        Credit = isCredit ? amount : 0m,
                        Debit = isCredit ? 0m : amount,
                        Balance = TryDecNullable(Prop(t, "currentBalance")),
                        Mode = (Prop(t, "mode") ?? "").ToUpperInvariant(),
                        AccountRef = accRef
                    });
                }

                var summary = acct.SelectToken("Summary") as JObject;
                r.Accounts.Add(new AaAccountInfo
                {
                    MaskedAccountNumber = string.IsNullOrWhiteSpace(accRef) ? "-" : accRef,
                    FipName = parentBody != null && fipNames.TryGetValue(parentBody, out var fn) ? fn : "-",
                    Type = Prop(acct, "type") ?? "-",
                    CurrentBalance = summary != null ? TryDecNullable(Prop(summary, "currentBalance")) : null,
                    Status = summary != null ? Prop(summary, "status") ?? "-" : "-",
                    Branch = summary != null ? Prop(summary, "branch") ?? "-" : "-",
                    IfscCode = summary != null ? Prop(summary, "ifscCode") ?? "-" : "-",
                    OpeningDate = summary != null ? Prop(summary, "openingDate") ?? "-" : "-",
                    TransactionCount = list.Count - before
                });
            }

            // ── Shape B: ledger feed — dataresponse → response → data → ledger → row[]
            foreach (var row in root.SelectTokens("$..ledger.row[*]").OfType<JObject>())
            {
                var id = Prop(row, "ledgerrowid", "trno") ?? Guid.NewGuid().ToString();
                if (!seen.Add("B:" + id)) continue;

                var dateStr = Prop(row, "trdate", "valuedate");
                if (!TryDate(dateStr, out var date)) continue;

                var deposit = Dec(Prop(row, "deposit"));
                var withdrawal = Dec(Prop(row, "withdrawal"));
                if (deposit == 0m && withdrawal == 0m) continue;   // malformed amount row

                var unpass = Prop(row, "isunpass");

                list.Add(new AaTransaction
                {
                    Date = date,
                    ValueDate = TryDate(Prop(row, "valuedate"), out var vd2) ? vd2 : null,
                    TxnNo = Prop(row, "trno", "ledgerrowid") ?? "",
                    Narration = Prop(row, "narration") ?? "",
                    Description = Prop(row, "description", "code") ?? "",
                    Credit = deposit,
                    Debit = withdrawal,
                    Balance = TryDecNullable(Prop(row, "closingbalance")),
                    Mode = (Prop(row, "modeoftransaction") ?? "").ToUpperInvariant(),
                    ChequeNo = Prop(row, "chequenumber") ?? "",
                    IsUnpassed = !string.IsNullOrWhiteSpace(unpass)
                                 && (unpass.Equals("true", StringComparison.OrdinalIgnoreCase)
                                     || unpass == "1"
                                     || unpass.Equals("Y", StringComparison.OrdinalIgnoreCase))
                });
            }

            return list;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PHASE 2 — classification
        // ═════════════════════════════════════════════════════════════════
        private static void Classify(AaTransaction t)
        {
            var text = $"{t.Narration} {t.Description} {t.Mode}".ToUpperInvariant();
            bool Has(params string[] keys) => keys.Any(k => text.Contains(k, StringComparison.Ordinal));

            // Whole-token match. Short tokens are dangerous as plain substrings —
            // "TORRENT POWER" contains RENT, "PREMIUM" contains EMI, "PUBLIC" contains
            // LIC — so anything short or ambiguous must be matched on boundaries.
            bool HasWord(params string[] keys) =>
                keys.Any(k => Regex.IsMatch(text, $@"(^|[^A-Z0-9]){Regex.Escape(k)}([^A-Z0-9]|$)"));

            var hasCheque = !string.IsNullOrWhiteSpace(t.ChequeNo);
            var isTransferMode = Has("NEFT", "RTGS", "IMPS", "UPI", "TRANSFER") || HasWord("FT");

            // Self transfer reads the same in both directions.
            //
            // Deliberately NOT matching a bare "SELF": real statements carry
            // "ATM WDL/SELF WITHDRAWAL" and "SELF CHEQUE", which are cash
            // withdrawals, not inter-account transfers. Own-account movement is
            // also frequently worded without "self" at all ("Transfer To Savings"),
            // so those phrasings are matched explicitly instead.
            if (Has("TO SELF", "OWN ACCOUNT", "OWN A/C", "INTERNAL TRANSFER", "SELF TRF",
                    "TRANSFER TO SAVINGS", "TRANSFER FROM SAVINGS", "TO SAVINGS", "FROM SAVINGS",
                    "TO OWN", "FROM OWN"))
            {
                t.Category = AaCategory.SelfTransfer;
                t.Confidence = Has("TO SELF", "OWN ACCOUNT", "INTERNAL TRANSFER") ? "High" : "Medium";
                return;
            }

            if (t.IsCredit)
            {
                if (Has("INTEREST", "INT.CR", "INT CR", "PERIODIC INTEREST"))
                { t.Category = AaCategory.InterestIncome; t.Confidence = "High"; return; }

                if (Has("CASH") || t.Mode == "CASH")
                { t.Category = AaCategory.CashDeposit; t.Confidence = "High"; return; }

                if (Has("SALARY", "PAYROLL"))
                { t.Category = AaCategory.Salary; t.Confidence = "High"; return; }

                if (Has("REFUND", "REVERSAL", "RETURN OF", "CASHBACK"))
                { t.Category = AaCategory.Refund; t.Confidence = "Medium"; return; }

                if (Has("LOAN", "DISBURS", "OD LIMIT", "CC LIMIT", "ADVANCE AGAINST", "OVERDRAFT"))
                { t.Category = AaCategory.LoanFinance; t.Confidence = "Medium"; return; }

                if (Has("MUTUAL FUND", "REDEMPTION", "MATURITY", "DIVIDEND", "INVESTMENT"))
                { t.Category = AaCategory.Investment; t.Confidence = "Medium"; return; }

                // An invoice reference is the strongest business-receipt evidence a
                // statement line can carry (e.g. "NEFT CR/ACME PVT LTD/INV-4523").
                if (Has("INV-", "INVOICE", "INV NO", "BILL NO"))
                { t.Category = AaCategory.BusinessReceipt; t.Confidence = "High"; return; }

                if (isTransferMode)
                { t.Category = AaCategory.TradeReceipt; t.Confidence = "Medium"; return; }

                // Clearing / generic transfer credits: plausibly business, not proven.
                if (Has("TRF", "TRNS", "CLG", "CLEARING", "CHQ DEP", "CHEQUE DEP", "LC"))
                { t.Category = AaCategory.BusinessReceipt; t.Confidence = "Low"; return; }

                t.Category = AaCategory.Unknown; t.Confidence = "Low";
                return;
            }

            // ── Debits
            if (Has("CHARGE", "CHRG", "COMMISSION", "PENAL", "SMS CHG", "AMB ", "MIN BAL", " FEE", "FEES"))
            { t.Category = AaCategory.BankCharges; t.Confidence = "High"; return; }

            if (Has("CHEQUE WITHDRAWAL", "SELF CHEQUE", "CASH WDL", "ATM", "CASH WITHDRAWAL")
                || t.Mode == "CASH" || (Has("CASH") && !Has("CASH DEP")))
            { t.Category = AaCategory.CashWithdrawal; t.Confidence = "High"; return; }

            // ── Purpose is checked before instrument.
            // Statements routinely carry the rail and the purpose in one string
            // ("ACH DR/OFFICE RENT/SKYLINE PROPERTIES"). Matching NACH/ECS first
            // would bury the far more informative "Rent".
            if (Has("SALARY", "PAYROLL"))
            { t.Category = AaCategory.Salary; t.Confidence = "High"; return; }

            if (Has("GST", "TDS", "CBDT", "INCOME TAX", "TAX PAY", "CHALLAN", "ITNS"))
            { t.Category = AaCategory.GstTax; t.Confidence = "High"; return; }

            // Utility before rent: "TORRENT POWER" is an electricity biller.
            if (Has("ELECTRIC", "MSEB", "TORRENT POWER", "BSNL", "AIRTEL", "JIO", "VODAFONE",
                    "WATER", "BROADBAND", "FIBERNET", "BILLPAY", "BILL PAY", "POSTPAID")
                || HasWord("GAS"))
            { t.Category = AaCategory.Utility; t.Confidence = "Medium"; return; }

            if (HasWord("RENT") || Has("RENT PAYMENT", "OFFICE RENT", "HOUSE RENT"))
            { t.Category = AaCategory.Rent; t.Confidence = "High"; return; }

            // Insurance before EMI — an insurance premium is a recurring mandate,
            // not a loan instalment.
            if (Has("INSURANCE", "PREMIUM") || HasWord("LIC"))
            { t.Category = AaCategory.NachEcs; t.Confidence = "Medium"; return; }

            if (HasWord("EMI") || Has("REPAY", "INSTALMENT", "INSTALLMENT", "CREDIT CARD PAYMENT", "CARD AUTOPAY")
                || HasWord("LOAN"))
            { t.Category = AaCategory.LoanEmi; t.Confidence = HasWord("EMI", "LOAN") ? "High" : "Medium"; return; }

            if (Has("NACH", "ECS", "MANDATE", "SI/") || HasWord("ACH"))
            { t.Category = AaCategory.NachEcs; t.Confidence = "High"; return; }

            if (Has("INTEREST"))
            { t.Category = AaCategory.InterestPaid; t.Confidence = "High"; return; }

            if (hasCheque || Has("CHEQUE", "CHQ"))
            { t.Category = AaCategory.ChequePayment; t.Confidence = hasCheque ? "High" : "Medium"; return; }

            // Outbound investments (e.g. "SIP/MUTUAL FUND/...") are neither a business
            // expense nor an obligation — they are discretionary, so they are reported
            // as Other rather than being folded into operating spend.
            if (Has("MUTUAL FUND", "SIP/", "INVESTMENT", "DEMAT", "BROKING"))
            { t.Category = AaCategory.Other; t.Confidence = "Medium"; return; }

            // "PURCHASE"/"VENDOR"/"SUPPLIER" on a transfer is direct trade-payment evidence.
            if (Has("PURCHASE", "VENDOR", "SUPPLIER", "PAYMENT TO"))
            { t.Category = AaCategory.BusinessExpense; t.Confidence = "Medium"; return; }

            if (isTransferMode)
            { t.Category = AaCategory.BusinessExpense; t.Confidence = "Low"; return; }

            t.Category = AaCategory.Unknown; t.Confidence = "Low";
        }

        /// <summary>Inflow buckets treated as operating business inflow.</summary>
        private static bool IsBusinessInflow(string category)
            => category is AaCategory.BusinessReceipt or AaCategory.TradeReceipt;

        // ═════════════════════════════════════════════════════════════════
        //  Sections 1-2 — summary and cash flow
        // ═════════════════════════════════════════════════════════════════
        private static void BuildSummary(AaAnalysisResult r, List<AaTransaction> t)
        {
            r.PeriodFrom = t.First().Date;
            r.PeriodTo = t.Last().Date;
            r.AnalysisPeriod = $"{r.PeriodFrom:dd MMM yyyy} – {r.PeriodTo:dd MMM yyyy}";

            r.TransactionCount = t.Count;
            r.CreditCount = t.Count(x => x.IsCredit);
            r.DebitCount = t.Count - r.CreditCount;
            r.TotalCredits = t.Sum(x => x.Credit);
            r.TotalDebits = t.Sum(x => x.Debit);

            r.MonthsCovered = Math.Max(1, t.Select(x => x.Date.ToString("yyyy-MM")).Distinct().Count());
            r.AvgMonthlyCredit = Round(r.TotalCredits / r.MonthsCovered);
            r.AvgMonthlyDebit = Round(r.TotalDebits / r.MonthsCovered);
            r.CreditToDebitRatio = r.TotalDebits > 0 ? Round(r.TotalCredits / r.TotalDebits, 2) : 0m;

            r.PotentialBusinessInflow = t.Where(x => x.IsCredit && IsBusinessInflow(x.Category)).Sum(x => x.Credit);
            r.PotentialSelfTransferCredits = t.Where(x => x.IsCredit && x.Category == AaCategory.SelfTransfer).Sum(x => x.Credit);
            r.InterestIncome = t.Where(x => x.IsCredit && x.Category == AaCategory.InterestIncome).Sum(x => x.Credit);
            r.OtherUnknownInflow = t.Where(x => x.IsCredit && x.Category is AaCategory.Other or AaCategory.Unknown).Sum(x => x.Credit);
            r.PotentialNonBusinessInflow = r.TotalCredits - r.PotentialBusinessInflow;
        }

        private static void BuildCategories(AaAnalysisResult r, List<AaTransaction> t)
        {
            r.InflowCategories = t.Where(x => x.IsCredit)
                .GroupBy(x => x.Category)
                .Select(g => new AaCategorySummary
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Credit),
                    Percentage = r.TotalCredits > 0 ? Round(g.Sum(x => x.Credit) / r.TotalCredits * 100m, 1) : 0m,
                    CountsAsBusinessInflow = IsBusinessInflow(g.Key)
                })
                .OrderByDescending(x => x.Amount).ToList();

            r.OutflowCategories = t.Where(x => !x.IsCredit)
                .GroupBy(x => x.Category)
                .Select(g => new AaCategorySummary
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.Debit),
                    Percentage = r.TotalDebits > 0 ? Round(g.Sum(x => x.Debit) / r.TotalDebits * 100m, 1) : 0m
                })
                .OrderByDescending(x => x.Amount).ToList();
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 6 — cash behaviour
        // ═════════════════════════════════════════════════════════════════
        private static void BuildCash(AaAnalysisResult r, List<AaTransaction> t)
        {
            var deposits = t.Where(x => x.IsCredit && x.Category == AaCategory.CashDeposit).ToList();
            var withdrawals = t.Where(x => !x.IsCredit && x.Category == AaCategory.CashWithdrawal).ToList();

            r.TotalCashDeposits = deposits.Sum(x => x.Credit);
            r.TotalCashWithdrawals = withdrawals.Sum(x => x.Debit);
            r.CashDepositCount = deposits.Count;
            r.CashWithdrawalCount = withdrawals.Count;
            r.CashDepositRatio = r.TotalCredits > 0 ? Round(r.TotalCashDeposits / r.TotalCredits * 100m, 1) : 0m;
            r.CashWithdrawalRatio = r.TotalDebits > 0 ? Round(r.TotalCashWithdrawals / r.TotalDebits * 100m, 1) : 0m;
            r.AvgCashWithdrawal = withdrawals.Count > 0 ? Round(r.TotalCashWithdrawals / withdrawals.Count) : 0m;
            r.LargestCashWithdrawal = withdrawals.Count > 0 ? withdrawals.Max(x => x.Debit) : 0m;
            r.LargestCashDeposit = deposits.Count > 0 ? deposits.Max(x => x.Credit) : 0m;

            // Repeated high-value withdrawals — factual observation, not an accusation.
            if (withdrawals.Count >= 3 && r.AvgCashWithdrawal > 0)
            {
                var high = withdrawals.Count(x => x.Debit >= r.AvgCashWithdrawal * 1.5m);
                if (high >= 3)
                    r.CashPatternNote = $"{high} cash withdrawals exceeded 1.5× the average withdrawal value " +
                                        $"({Inr(r.AvgCashWithdrawal)}). Repeated high-value cash withdrawal pattern observed.";
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 7 — self transfers
        // ═════════════════════════════════════════════════════════════════
        private static void BuildSelfTransfers(AaAnalysisResult r, List<AaTransaction> t)
        {
            var self = t.Where(x => x.Category == AaCategory.SelfTransfer).ToList();
            r.SelfTransferCount = self.Count;
            r.SelfTransferAmount = self.Sum(x => x.Amount);
            r.SelfTransferPctOfCredits = r.TotalCredits > 0
                ? Round(self.Sum(x => x.Credit) / r.TotalCredits * 100m, 1) : 0m;
            r.SelfTransferPctOfDebits = r.TotalDebits > 0
                ? Round(self.Sum(x => x.Debit) / r.TotalDebits * 100m, 1) : 0m;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 8 — pass-through / round-trip detection
        //  Credits are indexed by amount and binary-searched, so each debit only
        //  scans the narrow tolerance band instead of every credit.
        // ═════════════════════════════════════════════════════════════════
        private static void BuildPassThrough(AaAnalysisResult r, List<AaTransaction> t)
        {
            var credits = t.Where(x => x.IsCredit).OrderBy(x => x.Credit).ToList();
            var creditAmounts = credits.Select(x => x.Credit).ToArray();
            var consumed = new bool[credits.Count];
            var maxWindow = PassThroughWindows.Max();

            foreach (var debit in t.Where(x => !x.IsCredit).OrderBy(x => x.Date))
            {
                var tol = Math.Max(debit.Debit * PassThroughPctTolerance, PassThroughAbsTolerance);
                var lo = LowerBound(creditAmounts, debit.Debit - tol);

                int bestIdx = -1; int bestGap = int.MaxValue; decimal bestDiff = decimal.MaxValue;

                for (var i = lo; i < credits.Count && creditAmounts[i] <= debit.Debit + tol; i++)
                {
                    if (consumed[i]) continue;
                    var c = credits[i];

                    var gap = (debit.Date.Date - c.Date.Date).Days;
                    if (gap < 0 || gap > maxWindow) continue;

                    var diff = Math.Abs(c.Credit - debit.Debit);
                    if (gap < bestGap || (gap == bestGap && diff < bestDiff))
                    { bestIdx = i; bestGap = gap; bestDiff = diff; }
                }

                if (bestIdx < 0) continue;

                consumed[bestIdx] = true;
                var credit = credits[bestIdx];
                var relDiff = credit.Credit > 0 ? bestDiff / credit.Credit : 1m;

                var confidence = (bestDiff == 0m && bestGap <= 1) || (relDiff <= 0.01m && bestGap <= 3)
                    ? "High"
                    : bestGap <= 7 ? "Moderate" : "Low";

                // Money leaving as cash or a self transfer is the more material pattern.
                var riskLevel = debit.Category is AaCategory.CashWithdrawal or AaCategory.SelfTransfer
                    ? (confidence == "High" ? "High" : "Moderate")
                    : confidence == "High" ? "Moderate" : "Low";

                r.PassThroughEvents.Add(new AaPassThroughEvent
                {
                    CreditDate = credit.Date,
                    CreditAmount = credit.Credit,
                    CreditNarration = credit.Narration,
                    DebitDate = debit.Date,
                    DebitAmount = debit.Debit,
                    DebitNarration = debit.Narration,
                    DebitMode = string.IsNullOrWhiteSpace(debit.Mode) ? debit.Category : debit.Mode,
                    DaysGap = bestGap,
                    AmountDifference = bestDiff,
                    Confidence = confidence,
                    RiskLevel = riskLevel
                });

                credit.RiskFlag = "Potential pass-through";
                debit.RiskFlag = "Potential pass-through";
            }

            r.PassThroughEvents = r.PassThroughEvents.OrderBy(e => e.CreditDate).ToList();
            r.PotentialPassThroughAmount = r.PassThroughEvents.Sum(e => e.CreditAmount);
            r.PassThroughPctOfCredits = r.TotalCredits > 0
                ? Round(r.PotentialPassThroughAmount / r.TotalCredits * 100m, 1) : 0m;
            r.PassThroughPctOfBusinessInflow = r.PotentialBusinessInflow > 0
                ? Round(r.PotentialPassThroughAmount / r.PotentialBusinessInflow * 100m, 1) : 0m;

            r.PassThroughRiskLevel = r.PassThroughPctOfCredits >= 30m ? "High"
                : r.PassThroughPctOfCredits >= 12m ? "Moderate" : "Low";
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 9 — balance behaviour
        // ═════════════════════════════════════════════════════════════════
        private static void BuildBalance(AaAnalysisResult r, List<AaTransaction> t)
        {
            var balances = t.Where(x => x.Balance.HasValue).Select(x => x.Balance!.Value).ToList();
            r.BalanceAvailable = balances.Count > 0;
            if (!r.BalanceAvailable) return;

            r.AvgBalance = Round(balances.Average());
            r.MedianBalance = Round(Median(balances));
            r.MinBalance = balances.Min();
            r.MaxBalance = balances.Max();
            r.ClosingBalance = t.Last(x => x.Balance.HasValue).Balance;

            // "Low balance" is defined relative to the account's own run-rate.
            r.LowBalanceThreshold = Round(r.AvgMonthlyDebit / 30m);
            r.LowBalanceEvents = r.LowBalanceThreshold > 0
                ? balances.Count(b => b < r.LowBalanceThreshold) : 0;
            r.NegativeBalanceEvents = balances.Count(b => b < 0);
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 11 — recurring obligations
        // ═════════════════════════════════════════════════════════════════
        private static void BuildObligations(AaAnalysisResult r, List<AaTransaction> t)
        {
            var obligationCategories = new[]
            {
                AaCategory.LoanEmi, AaCategory.NachEcs, AaCategory.Rent,
                AaCategory.Utility, AaCategory.Salary, AaCategory.GstTax, AaCategory.InterestPaid
            };

            var groups = t.Where(x => !x.IsCredit)
                .GroupBy(x => $"{NarrationKey(x.Narration)}|{Bucket(x.Debit)}")
                .Where(g => g.Count() >= 3)
                .ToList();

            foreach (var g in groups)
            {
                var items = g.OrderBy(x => x.Date).ToList();
                var category = items.GroupBy(x => x.Category).OrderByDescending(x => x.Count()).First().Key;

                var intervals = items.Zip(items.Skip(1), (a, b) => (b.Date - a.Date).Days).Where(d => d > 0).ToList();
                var avgInterval = intervals.Count > 0 ? (int)Math.Round(intervals.Average()) : 0;

                // Recurring = an obligation-type category, or a steady ~monthly cadence.
                var looksMonthly = avgInterval >= 20 && avgInterval <= 40;
                if (!obligationCategories.Contains(category) && !looksMonthly) continue;

                r.Obligations.Add(new AaObligation
                {
                    Label = items.First().Narration.Trim() is { Length: > 0 } n
                        ? (n.Length > 60 ? n[..60] : n)
                        : category,
                    Category = category,
                    Occurrences = items.Count,
                    TypicalAmount = Round(Median(items.Select(x => x.Debit).ToList())),
                    TotalAmount = items.Sum(x => x.Debit),
                    AverageIntervalDays = avgInterval,
                    Confidence = obligationCategories.Contains(category) && looksMonthly ? "High"
                        : obligationCategories.Contains(category) ? "Medium" : "Low",
                    Transactions = items
                });
            }

            r.Obligations = r.Obligations.OrderByDescending(o => o.TotalAmount).ToList();
            r.TotalObligationAmount = r.Obligations.Sum(o => o.TotalAmount);
            // Monthly run-rate: spread the observed obligation total across the period.
            r.EstimatedMonthlyObligation = r.MonthsCovered > 0
                ? Round(r.TotalObligationAmount / r.MonthsCovered) : 0m;
            r.ObligationToBusinessInflowRatio = r.PotentialBusinessInflow > 0
                ? Round(r.TotalObligationAmount / r.PotentialBusinessInflow * 100m, 1) : 0m;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 12 — banking discipline
        // ═════════════════════════════════════════════════════════════════
        private static void BuildDiscipline(AaAnalysisResult r, List<AaTransaction> t)
        {
            r.DisciplineEvents = t.Where(x =>
            {
                if (x.IsUnpassed) return true;
                var s = $"{x.Narration} {x.Description}".ToUpperInvariant();
                return s.Contains("BOUNCE") || s.Contains("DISHONOUR") || s.Contains("DISHONOR")
                    || s.Contains("INSUFFICIENT") || s.Contains("RETURN CHG") || s.Contains("CHQ RTN")
                    || s.Contains("ECS RETURN") || s.Contains("NACH RETURN") || s.Contains("REJECT");
            }).ToList();

            foreach (var e in r.DisciplineEvents) e.RiskFlag ??= "Banking discipline event";

            r.BankChargesTotal = t.Where(x => x.Category == AaCategory.BankCharges).Sum(x => x.Debit);

            r.DisciplineNote = r.DisciplineEvents.Count == 0
                ? "No bounce event identified in available statement data."
                : $"{r.DisciplineEvents.Count} potential bounce/return event(s) identified.";
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 13 — frequency and behaviour
        // ═════════════════════════════════════════════════════════════════
        private static void BuildFrequency(AaAnalysisResult r, List<AaTransaction> t)
        {
            var amounts = t.Select(x => x.Amount).ToList();
            r.TxnPerMonth = Round((decimal)t.Count / r.MonthsCovered, 1);
            r.AvgTxnSize = Round(amounts.Average());
            r.MedianTxnSize = Round(Median(amounts));
            r.LargestTxn = amounts.Max();
            r.SmallestTxn = amounts.Min();

            var monthlyCounts = t.GroupBy(x => x.Date.ToString("yyyy-MM")).Select(g => (decimal)g.Count()).ToList();
            var cv = CoefficientOfVariation(monthlyCounts);

            r.ActivityPattern = r.TxnPerMonth < 5 ? "Low Activity"
                : cv > 0.6m ? "Irregular"
                : cv > 0.35m ? "Moderately Variable"
                : "Regular";
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 14 — concentration
        // ═════════════════════════════════════════════════════════════════
        private static void BuildConcentration(AaAnalysisResult r, List<AaTransaction> t)
        {
            var credits = t.Where(x => x.IsCredit).OrderByDescending(x => x.Credit).ToList();
            var debits = t.Where(x => !x.IsCredit).OrderByDescending(x => x.Debit).ToList();

            r.TopCredits = credits.Take(5).ToList();
            r.TopDebits = debits.Take(5).ToList();

            if (r.TotalCredits > 0)
            {
                r.Top5CreditPct = Round(credits.Take(5).Sum(x => x.Credit) / r.TotalCredits * 100m, 1);
                r.Top10CreditPct = Round(credits.Take(10).Sum(x => x.Credit) / r.TotalCredits * 100m, 1);
            }
            if (r.TotalDebits > 0)
                r.Top5DebitPct = Round(debits.Take(5).Sum(x => x.Debit) / r.TotalDebits * 100m, 1);

            foreach (var g in t.GroupBy(x => (x.Direction, x.Amount))
                               .Where(g => g.Count() >= 3 && g.Key.Amount > 0)
                               .OrderByDescending(g => g.Key.Amount * g.Count())
                               .Take(5))
            {
                r.RepeatedValueNotes.Add(
                    $"{g.Count()} × {Inr(g.Key.Amount)} {g.Key.Direction.ToLowerInvariant()} transactions of identical value.");
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 15 — monthly trend
        // ═════════════════════════════════════════════════════════════════
        private static void BuildMonthly(AaAnalysisResult r, List<AaTransaction> t)
        {
            var passByMonth = r.PassThroughEvents
                .GroupBy(e => e.CreditDate.ToString("yyyy-MM"))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.CreditAmount));

            var obligationTxnIds = new HashSet<AaTransaction>(r.Obligations.SelectMany(o => o.Transactions));

            foreach (var g in t.GroupBy(x => x.Date.ToString("yyyy-MM")).OrderBy(g => g.Key))
            {
                var withBalance = g.Where(x => x.Balance.HasValue).ToList();
                var row = new AaMonthlyRow
                {
                    Month = g.Key,
                    MonthLabel = DateTime.ParseExact(g.Key, "yyyy-MM", CultureInfo.InvariantCulture).ToString("MMM yyyy"),
                    Credits = g.Sum(x => x.Credit),
                    BusinessCredits = g.Where(x => x.IsCredit && IsBusinessInflow(x.Category)).Sum(x => x.Credit),
                    SelfTransfers = g.Where(x => x.Category == AaCategory.SelfTransfer).Sum(x => x.Amount),
                    PassThrough = passByMonth.TryGetValue(g.Key, out var p) ? p : 0m,
                    Debits = g.Sum(x => x.Debit),
                    CashWithdrawals = g.Where(x => x.Category == AaCategory.CashWithdrawal).Sum(x => x.Debit),
                    Obligations = g.Where(x => obligationTxnIds.Contains(x)).Sum(x => x.Debit),
                    ClosingBalance = withBalance.Count > 0 ? withBalance.Last().Balance : null,
                    AverageBalance = withBalance.Count > 0 ? Round(withBalance.Average(x => x.Balance!.Value)) : null,
                    TxnCount = g.Count()
                };

                if (row.Credits > 0 && row.PassThrough / row.Credits >= 0.3m) row.RiskFlag = "High pass-through";
                else if (row.NetCashFlow < 0) row.RiskFlag = "Negative net flow";
                else if (row.Debits > 0 && row.CashWithdrawals / row.Debits >= 0.5m) row.RiskFlag = "Cash heavy";

                r.Monthly.Add(row);
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 10 — liquidity (every ratio carries its formula)
        // ═════════════════════════════════════════════════════════════════
        private static void BuildLiquidity(AaAnalysisResult r)
        {
            if (r.BalanceAvailable && r.AvgMonthlyDebit > 0)
            {
                var cover = Round(r.AvgBalance / r.AvgMonthlyDebit, 2);
                r.LiquidityMetrics.Add(new AaLiquidityMetric
                {
                    Metric = "Balance Cover",
                    Formula = "Average Balance ÷ Average Monthly Debit",
                    Value = $"{cover:0.00}×",
                    RiskCategory = cover >= 1m ? "Low" : cover >= 0.35m ? "Moderate" : "High",
                    Interpretation = cover >= 1m
                        ? "Maintained balance covers a full month of outflows."
                        : $"Maintained balance covers roughly {cover * 30m:0} day(s) of outflows."
                });

                var minCover = Round(r.MinBalance / r.AvgMonthlyDebit, 2);
                r.LiquidityMetrics.Add(new AaLiquidityMetric
                {
                    Metric = "Minimum Balance Cover",
                    Formula = "Minimum Balance ÷ Average Monthly Debit",
                    Value = $"{minCover:0.00}×",
                    RiskCategory = minCover >= 0.25m ? "Low" : minCover >= 0.05m ? "Moderate" : "High",
                    Interpretation = "Worst-case liquidity observed during the statement period."
                });
            }

            r.LiquidityMetrics.Add(new AaLiquidityMetric
            {
                Metric = "Credit-to-Debit Ratio",
                Formula = "Total Credits ÷ Total Debits",
                Value = $"{r.CreditToDebitRatio:0.00}",
                RiskCategory = r.CreditToDebitRatio >= 1.05m ? "Low" : r.CreditToDebitRatio >= 0.98m ? "Moderate" : "High",
                Interpretation = r.CreditToDebitRatio >= 1m
                    ? "Inflows exceed outflows over the period."
                    : "Outflows exceed inflows over the period."
            });

            if (r.TotalCredits > 0)
            {
                var netRatio = Round(r.NetCashFlow / r.TotalCredits * 100m, 1);
                r.LiquidityMetrics.Add(new AaLiquidityMetric
                {
                    Metric = "Net Cash Flow Ratio",
                    Formula = "(Total Credits − Total Debits) ÷ Total Credits",
                    Value = $"{netRatio:0.0}%",
                    RiskCategory = netRatio >= 5m ? "Low" : netRatio >= 0m ? "Moderate" : "High",
                    Interpretation = "Share of inflow retained in the account over the period."
                });
            }

            if (r.PotentialBusinessInflow > 0 && r.EstimatedMonthlyObligation > 0)
            {
                var burden = Round(r.EstimatedMonthlyObligation / (r.PotentialBusinessInflow / r.MonthsCovered) * 100m, 1);
                r.LiquidityMetrics.Add(new AaLiquidityMetric
                {
                    Metric = "Obligation Burden",
                    Formula = "Estimated Monthly Obligation ÷ Average Monthly Potential Business Inflow",
                    Value = $"{burden:0.0}%",
                    RiskCategory = burden <= 35m ? "Low" : burden <= 60m ? "Moderate" : "High",
                    Interpretation = "Share of potential business inflow already committed to recurring outflows."
                });
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 16 — sustainable inflow
        // ═════════════════════════════════════════════════════════════════
        private static void BuildSustainable(AaAnalysisResult r)
        {
            var deductions = r.PotentialSelfTransferCredits + r.InterestIncome
                + r.InflowCategories.Where(c => c.Category is AaCategory.LoanFinance or AaCategory.Investment or AaCategory.Refund)
                                    .Sum(c => c.Amount);

            var baseInflow = r.TotalCredits - deductions;
            var sustainable = baseInflow - r.PotentialPassThroughAmount;
            r.EstimatedSustainableInflow = Math.Max(0m, Round(sustainable));

            r.SustainableMethodology.Add($"Gross credits: {Inr(r.TotalCredits)}");
            r.SustainableMethodology.Add($"Less potential self transfers: {Inr(r.PotentialSelfTransferCredits)}");
            r.SustainableMethodology.Add($"Less interest income: {Inr(r.InterestIncome)}");
            r.SustainableMethodology.Add($"Less loan / investment / refund inflows: " +
                $"{Inr(r.InflowCategories.Where(c => c.Category is AaCategory.LoanFinance or AaCategory.Investment or AaCategory.Refund).Sum(c => c.Amount))}");
            r.SustainableMethodology.Add($"Less potential pass-through credits: {Inr(r.PotentialPassThroughAmount)}");
            r.SustainableMethodology.Add("Result is an estimate from bank data only — not audited turnover.");
        }

        // ═════════════════════════════════════════════════════════════════
        //  Explainable score (8 weighted components)
        // ═════════════════════════════════════════════════════════════════
        private static void BuildScore(AaAnalysisResult r)
        {
            void Add(string name, int score, int weight, string reason)
                => r.ScoreComponents.Add(new AaScoreComponent
                { Name = name, Score = Math.Clamp(score, 0, 100), Weight = weight, Reason = reason });

            // 1. Cash flow quality
            var cf = r.NetCashFlow >= 0 ? 75 : 45;
            if (r.CreditToDebitRatio >= 1.1m) cf += 15; else if (r.CreditToDebitRatio < 0.95m) cf -= 15;
            Add("Cash Flow Quality", cf, 15,
                $"Net cash flow {Inr(r.NetCashFlow)}; credit-to-debit ratio {r.CreditToDebitRatio:0.00}.");

            // 2. Inflow quality
            var bizPct = r.TotalCredits > 0 ? r.PotentialBusinessInflow / r.TotalCredits * 100m : 0m;
            Add("Inflow Quality", (int)Math.Round(bizPct), 18,
                $"{bizPct:0.0}% of credits classify as potential business inflow; " +
                $"{Inr(r.PotentialNonBusinessInflow)} is non-business or unclassified.");

            // 3. Pass-through risk (inverted — less is better)
            var ptScore = 100 - (int)Math.Round(Math.Min(100m, r.PassThroughPctOfCredits * 2.5m));
            Add("Pass-through Risk", ptScore, 16,
                r.PassThroughEvents.Count == 0
                    ? "No credit was followed by a similar-value debit inside the detection window."
                    : $"{r.PassThroughEvents.Count} potential pass-through event(s) covering {r.PassThroughPctOfCredits:0.0}% of credits.");

            // 4. Balance stability
            int bal;
            string balReason;
            if (!r.BalanceAvailable) { bal = 50; balReason = "Balance not reported in the statement data — scored neutral."; }
            else
            {
                var cover = r.AvgMonthlyDebit > 0 ? r.AvgBalance / r.AvgMonthlyDebit : 1m;
                bal = (int)Math.Round(Math.Min(100m, cover * 100m));
                if (r.NegativeBalanceEvents > 0) bal -= 20;
                balReason = $"Average balance {Inr(r.AvgBalance)} against average monthly debit {Inr(r.AvgMonthlyDebit)}; " +
                            $"minimum balance {Inr(r.MinBalance)}.";
            }
            Add("Balance Stability", bal, 15, balReason);

            // 5. Banking discipline
            var disc = r.DisciplineEvents.Count == 0 ? 95 : Math.Max(20, 95 - r.DisciplineEvents.Count * 15);
            Add("Banking Discipline", disc, 12, r.DisciplineNote);

            // 6. Obligation burden (inverted)
            int obl;
            if (r.PotentialBusinessInflow <= 0) { obl = 50; }
            else
            {
                var burden = r.EstimatedMonthlyObligation / Math.Max(1m, r.PotentialBusinessInflow / r.MonthsCovered) * 100m;
                obl = 100 - (int)Math.Round(Math.Min(100m, burden));
            }
            Add("Obligation Burden", obl, 12,
                $"{r.Obligations.Count} recurring obligation group(s); estimated monthly commitment {Inr(r.EstimatedMonthlyObligation)}.");

            // 7. Transaction consistency
            var consistency = r.ActivityPattern switch
            {
                "Regular" => 90,
                "Moderately Variable" => 70,
                "Irregular" => 45,
                _ => 35
            };
            Add("Transaction Consistency", consistency, 7,
                $"{r.TxnPerMonth:0.0} transactions/month across {r.MonthsCovered} month(s) — {r.ActivityPattern.ToLowerInvariant()} activity.");

            // 8. Cash dependency (inverted)
            var cashScore = 100 - (int)Math.Round(Math.Min(100m, r.CashWithdrawalRatio * 1.5m));
            Add("Cash Dependency", cashScore, 5,
                $"Cash withdrawals are {r.CashWithdrawalRatio:0.0}% of total debits.");

            var totalWeight = r.ScoreComponents.Sum(c => c.Weight);
            r.HealthScore = totalWeight > 0
                ? (int)Math.Round(r.ScoreComponents.Sum(c => (decimal)c.Score * c.Weight) / totalWeight)
                : 0;

            r.RiskGrade = r.HealthScore >= 80 ? "Low" : r.HealthScore >= 65 ? "Moderate"
                : r.HealthScore >= 50 ? "Elevated" : "High";

            var drivers = r.ScoreComponents.Where(c => c.Score < 60)
                .OrderBy(c => c.Score).Take(3)
                .Select(c => c.Name.ToLowerInvariant()).ToList();
            r.ScoreExplanation = drivers.Count > 0
                ? $"Primary risk drivers: {string.Join(", ", drivers)}."
                : "No individual component scored below the concern threshold.";
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 16 — rule-based risk flags (only when evidence supports)
        // ═════════════════════════════════════════════════════════════════
        private static void BuildRiskFlags(AaAnalysisResult r)
        {
            void Flag(string sev, string title, string evidence, string relevance)
                => r.RiskFlags.Add(new AaRiskFlag { Severity = sev, Title = title, Evidence = evidence, CreditRelevance = relevance });

            if (r.PassThroughPctOfCredits >= 30m)
                Flag("High", "High potential pass-through behaviour",
                    $"{r.PassThroughEvents.Count} event(s) totalling {Inr(r.PotentialPassThroughAmount)} — {r.PassThroughPctOfCredits:0.0}% of credits.",
                    "Gross bank credits may overstate sustainable business cash flow.");
            else if (r.PassThroughPctOfCredits >= 12m)
                Flag("Medium", "Potential pass-through behaviour observed",
                    $"{r.PassThroughEvents.Count} event(s) totalling {Inr(r.PotentialPassThroughAmount)}.",
                    "A portion of credits may not represent retained business receipts.");

            if (r.CashWithdrawalRatio >= 50m)
                Flag("High", "Very high cash withdrawal dependency",
                    $"Cash withdrawals are {r.CashWithdrawalRatio:0.0}% of total debits ({Inr(r.TotalCashWithdrawals)}).",
                    "Limits verifiability of end-use of funds.");
            else if (r.CashWithdrawalRatio >= 30m)
                Flag("Medium", "Elevated cash withdrawal activity",
                    $"Cash withdrawals are {r.CashWithdrawalRatio:0.0}% of total debits.",
                    "Reduces traceability of business expenditure.");

            if (r.BalanceAvailable && r.NegativeBalanceEvents > 0)
                Flag("High", "Negative balance observed",
                    $"{r.NegativeBalanceEvents} transaction(s) closed with a negative balance; minimum {Inr(r.MinBalance)}.",
                    "Indicates liquidity stress or overdraft utilisation.");

            if (r.DisciplineEvents.Count > 0)
                Flag("High", "Banking discipline events identified",
                    $"{r.DisciplineEvents.Count} bounce/return/unpassed event(s) in the statement.",
                    "Direct indicator of repayment behaviour risk.");

            if (r.ObligationToBusinessInflowRatio >= 60m)
                Flag("High", "High existing obligation burden",
                    $"Recurring obligations equal {r.ObligationToBusinessInflowRatio:0.0}% of potential business inflow.",
                    "Limited headroom for additional EMI.");

            if (r.OtherUnknownInflow > 0 && r.TotalCredits > 0
                && r.OtherUnknownInflow / r.TotalCredits >= 0.25m)
                Flag("Medium", "Significant unclassified inflows",
                    $"{Inr(r.OtherUnknownInflow)} ({r.OtherUnknownInflow / r.TotalCredits * 100m:0.0}% of credits) could not be classified.",
                    "Source of funds requires verification.");

            if (r.Top5CreditPct >= 60m)
                Flag("Medium", "High credit concentration",
                    $"Top 5 credits represent {r.Top5CreditPct:0.0}% of total credits.",
                    "Inflow depends on a small number of large transactions.");

            if (r.ActivityPattern is "Irregular" or "Low Activity")
                Flag("Medium", $"{r.ActivityPattern} banking activity",
                    $"{r.TxnPerMonth:0.0} transactions per month across {r.MonthsCovered} month(s).",
                    "Harder to establish a stable business cash-flow pattern.");

            // ── Positive indicators
            if (r.DisciplineEvents.Count == 0)
                Flag("Positive", "No bounce event identified",
                    "No cheque/NACH/ECS return or unpassed transaction found in available data.",
                    "Supports repayment discipline.");

            if (r.NetCashFlow > 0)
                Flag("Positive", "Positive net cash flow",
                    $"Credits exceeded debits by {Inr(r.NetCashFlow)} over the period.",
                    "Indicates the account retains a surplus.");

            if (r.ActivityPattern == "Regular")
                Flag("Positive", "Regular transaction activity",
                    $"{r.TxnPerMonth:0.0} transactions per month with consistent monthly volume.",
                    "Consistent with an operating business account.");

            if (r.Obligations.Count > 0 && r.ObligationToBusinessInflowRatio > 0 && r.ObligationToBusinessInflowRatio < 35m)
                Flag("Positive", "Manageable obligation burden",
                    $"Recurring obligations are {r.ObligationToBusinessInflowRatio:0.0}% of potential business inflow.",
                    "Headroom available for additional servicing.");
        }

        // ═════════════════════════════════════════════════════════════════
        //  Section 17 — credit analyst view
        // ═════════════════════════════════════════════════════════════════
        private static void BuildAnalystView(AaAnalysisResult r)
        {
            var v = r.CreditAnalystView;

            v.Add($"Apparent sustainable inflow: estimated {Inr(r.EstimatedSustainableInflow)} over {r.MonthsCovered} month(s) " +
                  $"(~{Inr(r.MonthsCovered > 0 ? r.EstimatedSustainableInflow / r.MonthsCovered : 0m)}/month) against gross credits of {Inr(r.TotalCredits)}.");

            v.Add($"Non-business share of credits: {Inr(r.PotentialNonBusinessInflow)} " +
                  $"({(r.TotalCredits > 0 ? r.PotentialNonBusinessInflow / r.TotalCredits * 100m : 0m):0.0}%), including " +
                  $"{Inr(r.PotentialSelfTransferCredits)} potential self transfers and {Inr(r.InterestIncome)} interest.");

            v.Add(r.PassThroughEvents.Count == 0
                ? "Pass-through behaviour: no credit was followed by a similar-value debit within 15 days."
                : $"Pass-through behaviour: {r.PassThroughEvents.Count} potential event(s) totalling {Inr(r.PotentialPassThroughAmount)} " +
                  $"({r.PassThroughPctOfCredits:0.0}% of credits) — risk level {r.PassThroughRiskLevel}.");

            v.Add(r.BalanceAvailable
                ? $"Balance stability: average {Inr(r.AvgBalance)}, median {Inr(r.MedianBalance)}, minimum {Inr(r.MinBalance)}, closing {Inr(r.ClosingBalance ?? 0m)}."
                : "Balance stability: unable to determine — the statement does not report running balances.");

            v.Add(r.Obligations.Count == 0
                ? "Recurring obligations: none identified with sufficient confidence."
                : $"Recurring obligations: {r.Obligations.Count} group(s), estimated {Inr(r.EstimatedMonthlyObligation)}/month " +
                  $"({r.ObligationToBusinessInflowRatio:0.0}% of potential business inflow).");

            v.Add($"Cash behaviour: {r.CashWithdrawalCount} withdrawal(s) totalling {Inr(r.TotalCashWithdrawals)} " +
                  $"({r.CashWithdrawalRatio:0.0}% of debits); {r.CashDepositCount} deposit(s) totalling {Inr(r.TotalCashDeposits)}.");

            v.Add($"Banking discipline: {r.DisciplineNote}");

            v.Add($"Inflow consistency: {r.ActivityPattern.ToLowerInvariant()} activity at {r.TxnPerMonth:0.0} transactions/month; " +
                  $"top 5 credits are {r.Top5CreditPct:0.0}% of total credits.");

            var risks = r.RiskFlags.Where(f => f.Severity != "Positive").Select(f => f.Title).Take(4).ToList();
            v.Add(risks.Count > 0 ? $"Major risk factors: {string.Join("; ", risks)}." : "Major risk factors: none identified from the statement data.");

            var pos = r.RiskFlags.Where(f => f.Severity == "Positive").Select(f => f.Title).Take(4).ToList();
            v.Add(pos.Count > 0 ? $"Positive factors: {string.Join("; ", pos)}." : "Positive factors: none identified from the statement data.");
        }

        // ═════════════════════════════════════════════════════════════════
        //  AI payload / response (reuses the existing FinRiskAI chat service)
        // ═════════════════════════════════════════════════════════════════
        public string BuildAiPayload(AaAnalysisResult r)
        {
            // Only computed metrics and bounded samples travel to the model —
            // never the full statement.
            var payload = new
            {
                analysisPeriod = r.AnalysisPeriod,
                monthsCovered = r.MonthsCovered,
                totals = new
                {
                    transactions = r.TransactionCount,
                    credits = r.TotalCredits,
                    debits = r.TotalDebits,
                    netCashFlow = r.NetCashFlow,
                    avgMonthlyCredit = r.AvgMonthlyCredit,
                    avgMonthlyDebit = r.AvgMonthlyDebit,
                    creditToDebitRatio = r.CreditToDebitRatio
                },
                balance = r.BalanceAvailable
                    ? new { available = true, avg = r.AvgBalance, median = r.MedianBalance, min = r.MinBalance, max = r.MaxBalance, closing = r.ClosingBalance, negativeEvents = r.NegativeBalanceEvents }
                    : (object)new { available = false },
                inflowSplit = new
                {
                    potentialBusinessInflow = r.PotentialBusinessInflow,
                    potentialNonBusinessInflow = r.PotentialNonBusinessInflow,
                    selfTransfers = r.PotentialSelfTransferCredits,
                    interest = r.InterestIncome,
                    otherUnknown = r.OtherUnknownInflow,
                    estimatedSustainableInflow = r.EstimatedSustainableInflow
                },
                inflowCategories = r.InflowCategories.Select(c => new { c.Category, c.Count, c.Amount, c.Percentage }),
                outflowCategories = r.OutflowCategories.Select(c => new { c.Category, c.Count, c.Amount, c.Percentage }),
                cash = new
                {
                    r.TotalCashDeposits,
                    r.TotalCashWithdrawals,
                    r.CashWithdrawalRatio,
                    r.LargestCashWithdrawal,
                    note = r.CashPatternNote
                },
                passThrough = new
                {
                    eventCount = r.PassThroughEvents.Count,
                    amount = r.PotentialPassThroughAmount,
                    pctOfCredits = r.PassThroughPctOfCredits,
                    riskLevel = r.PassThroughRiskLevel,
                    // A bounded sample of the strongest matches only.
                    events = r.PassThroughEvents
                        .OrderByDescending(e => e.CreditAmount).Take(20)
                        .Select(e => new
                        {
                            creditDate = e.CreditDate.ToString("yyyy-MM-dd"),
                            e.CreditAmount,
                            e.CreditNarration,
                            debitDate = e.DebitDate.ToString("yyyy-MM-dd"),
                            e.DebitAmount,
                            e.DebitNarration,
                            e.DaysGap,
                            e.Confidence
                        })
                },
                obligations = r.Obligations.Take(15).Select(o => new
                {
                    o.Label,
                    o.Category,
                    o.Occurrences,
                    o.TypicalAmount,
                    o.TotalAmount,
                    o.AverageIntervalDays,
                    o.Confidence
                }),
                estimatedMonthlyObligation = r.EstimatedMonthlyObligation,
                obligationToBusinessInflowRatio = r.ObligationToBusinessInflowRatio,
                bankingDiscipline = new
                {
                    note = r.DisciplineNote,
                    eventCount = r.DisciplineEvents.Count,
                    bankCharges = r.BankChargesTotal,
                    events = r.DisciplineEvents.Take(10).Select(e => new
                    {
                        date = e.Date.ToString("yyyy-MM-dd"),
                        e.Narration,
                        amount = e.Amount
                    })
                },
                concentration = new { r.Top5CreditPct, r.Top10CreditPct, r.Top5DebitPct, repeated = r.RepeatedValueNotes },
                frequency = new { r.TxnPerMonth, r.AvgTxnSize, r.MedianTxnSize, r.LargestTxn, r.SmallestTxn, r.ActivityPattern },
                monthlyTrend = r.Monthly.Select(m => new
                {
                    m.Month,
                    m.Credits,
                    m.BusinessCredits,
                    m.SelfTransfers,
                    m.PassThrough,
                    m.Debits,
                    m.CashWithdrawals,
                    m.Obligations,
                    m.ClosingBalance,
                    m.NetCashFlow
                }),
                deterministicScore = new { r.HealthScore, r.RiskGrade, components = r.ScoreComponents },
                deterministicRiskFlags = r.RiskFlags.Select(f => new { f.Severity, f.Title, f.Evidence }),
                // Representative transactions: largest credits and debits only.
                sampleTransactions = r.TopCredits.Concat(r.TopDebits).Select(x => new
                {
                    date = x.Date.ToString("yyyy-MM-dd"),
                    x.Direction,
                    amount = x.Amount,
                    x.Narration,
                    x.Category,
                    x.Confidence
                })
            };

            return JsonConvert.SerializeObject(payload);
        }

        public AaAiInsights ParseAiResponse(string? rawResponse)
        {
            var ai = new AaAiInsights();

            if (string.IsNullOrWhiteSpace(rawResponse))
            {
                ai.Error = "The AI service returned an empty response.";
                return ai;
            }

            try
            {
                // Models often wrap JSON in prose or ``` fences — take the outermost object.
                var start = rawResponse.IndexOf('{');
                var end = rawResponse.LastIndexOf('}');
                if (start < 0 || end <= start)
                {
                    ai.Error = "The AI response did not contain structured JSON.";
                    return ai;
                }

                var o = JObject.Parse(rawResponse.Substring(start, end - start + 1));

                string S(string name) => o.Value<string>(name)?.Trim() ?? "";
                List<string> L(string name) => o[name] is JArray a
                    ? a.Select(x => x.Type == JTokenType.Object ? x.ToString() : x.ToString().Trim())
                       .Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string>();

                ai.OverallAssessment = S("overallAssessment");
                ai.RiskLevel = S("riskLevel");
                ai.KeyStrengths = L("keyStrengths");
                ai.KeyConcerns = L("keyConcerns");
                ai.CashFlowAssessment = S("cashFlowAssessment");
                ai.InflowAssessment = S("inflowAssessment");
                ai.OutflowAssessment = S("outflowAssessment");
                ai.PassThroughAssessment = S("passThroughAssessment");
                ai.ObligationAssessment = S("obligationAssessment");
                ai.BalanceAssessment = S("balanceAssessment");
                ai.BankingDisciplineAssessment = S("bankingDisciplineAssessment");
                ai.CreditAnalystSummary = S("creditAnalystSummary");
                ai.RecommendedVerification = L("recommendedVerification");

                // Guard against an empty-but-parseable object.
                if (string.IsNullOrWhiteSpace(ai.OverallAssessment) && string.IsNullOrWhiteSpace(ai.CreditAnalystSummary))
                {
                    ai.Error = "The AI response did not contain a usable assessment.";
                    return ai;
                }

                if (ai.RiskLevel is not ("Low" or "Moderate" or "High")) ai.RiskLevel = "";
                ai.Available = true;
            }
            catch (JsonException)
            {
                ai.Error = "The AI response could not be parsed as JSON.";
            }

            return ai;
        }

        // ═════════════════════════════════════════════════════════════════
        //  Helpers
        // ═════════════════════════════════════════════════════════════════
        private static string? Prop(JObject o, params string[] names)
        {
            foreach (var n in names)
            {
                var p = o.Properties().FirstOrDefault(x => string.Equals(x.Name, n, StringComparison.OrdinalIgnoreCase));
                if (p == null || p.Value.Type is JTokenType.Null or JTokenType.Undefined) continue;
                var s = p.Value.Type is JTokenType.Object or JTokenType.Array ? null : p.Value.ToString();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
            }
            return null;
        }

        private static bool TryDate(string? value, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return true;
            return DateTime.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static decimal Dec(string? value) => TryDecNullable(value) ?? 0m;

        private static decimal? TryDecNullable(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var cleaned = Regex.Replace(value, @"[^\d.\-]", "");
            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        private static decimal Round(decimal v, int dp = 0) => Math.Round(v, dp, MidpointRounding.AwayFromZero);

        private static decimal Median(List<decimal> values)
        {
            if (values.Count == 0) return 0m;
            var s = values.OrderBy(x => x).ToList();
            var mid = s.Count / 2;
            return s.Count % 2 == 1 ? s[mid] : (s[mid - 1] + s[mid]) / 2m;
        }

        private static decimal CoefficientOfVariation(List<decimal> values)
        {
            if (values.Count < 2) return 0m;
            var mean = values.Average();
            if (mean == 0m) return 0m;
            var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            return (decimal)Math.Sqrt((double)variance) / mean;
        }

        /// <summary>First index whose value is >= target (binary search).</summary>
        private static int LowerBound(decimal[] sorted, decimal target)
        {
            int lo = 0, hi = sorted.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (sorted[mid] < target) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        /// <summary>Narration reduced to a stable key so recurring payments group together.</summary>
        private static string NarrationKey(string narration)
        {
            var s = Regex.Replace((narration ?? "").ToUpperInvariant(), @"[0-9]+", " ");
            s = Regex.Replace(s, @"[^A-Z ]", " ");
            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(3);
            return string.Join(" ", tokens);
        }

        /// <summary>Amounts bucketed to ~5% bands so near-identical instalments group.</summary>
        private static long Bucket(decimal amount) => amount <= 0 ? 0 : (long)Math.Round((double)amount / Math.Max(1d, (double)amount * 0.05d));

        private static string Inr(decimal amount)
        {
            var abs = Math.Abs(amount);
            if (abs >= 1_00_00_000m) return $"₹{amount / 1_00_00_000m:0.##} Cr";
            if (abs >= 1_00_000m) return $"₹{amount / 1_00_000m:0.##} L";
            return $"₹{amount:N0}";
        }
    }
}
