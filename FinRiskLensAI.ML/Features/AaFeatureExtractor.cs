using System.Globalization;
using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Models.Facts;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.ML.Features
{
    /// <summary>
    /// Extracts cash-flow, UPI transaction-quality, debt, and bank-behaviour features
    /// from the Account Aggregator FI data response (body[] → fiObjects[] →
    /// Summary/Transactions). Covers the underwriting deep-dive: AMB, peak balance,
    /// cash deposits, mode split, salary credits, supplier/customer classification,
    /// cheque vs ECS/NACH returns, OD limits and balance breaches.
    /// </summary>
    public class AaFeatureExtractor
    {
        /// <summary>Balance snapshots below this are counted as minimum-balance breaches.</summary>
        private const double MinBalanceThreshold = 10_000;

        private static readonly Regex BouncePattern = new("RETURN|BOUNCE|DISHONOU?R|INSUFF", RegexOptions.IgnoreCase);
        private static readonly Regex ChequeReturnPattern = new(@"(CHQ|CHEQUE).{0,20}(RETURN|BOUNCE|DISHONOU?R)|(RETURN|BOUNCE|DISHONOU?R).{0,20}(CHQ|CHEQUE)", RegexOptions.IgnoreCase);
        private static readonly Regex EcsNachReturnPattern = new(@"(ECS|NACH|ACH).{0,20}(RETURN|FAIL|BOUNCE)|(RETURN|FAIL|BOUNCE).{0,20}(ECS|NACH|ACH)", RegexOptions.IgnoreCase);
        private static readonly Regex EmiPattern = new(@"\bEMI\b|\bLOAN\b|NACH.*LOAN", RegexOptions.IgnoreCase);
        private static readonly Regex SalaryPattern = new(@"\bSALARY\b|\bSAL\b.{0,5}CREDIT", RegexOptions.IgnoreCase);
        private static readonly Regex CashDepositPattern = new(@"CASH.{0,10}(DEP|DEPOSIT)|\bCDM\b|BY CASH", RegexOptions.IgnoreCase);
        private static readonly Regex InterestPattern = new(@"\bINT(ERES)?T?\b.{0,10}(CREDIT|PAID)|\bINTEREST\b", RegexOptions.IgnoreCase);
        private static readonly Regex SupplierPattern = new(@"SUPPLIER|VENDOR|PURCHASE|PAYMENT|INVOICE|BILL PAY", RegexOptions.IgnoreCase);
        private static readonly Regex NonBusinessDebitPattern = new(@"\bEMI\b|\bLOAN\b|RENT|ELECTRICITY|SALARY|\bTAX\b|SIP|MUTUAL", RegexOptions.IgnoreCase);

        public void Extract(string aaJson, MsmeFeatureSet features)
        {
            if (string.IsNullOrWhiteSpace(aaJson)) return;

            var root = JObject.Parse(aaJson);
            var accounts = root.SelectTokens("body[*].fiObjects[*]").OfType<JObject>().ToList();
            if (accounts.Count == 0) return;

            features.HasAa = true;

            double totalBalance = 0, totalOdLimit = 0;
            var seenTxnIds = new HashSet<string>();
            var transactions = new List<(DateTime Date, string Type, string Mode, double Amount, string Narration, double? Balance)>();

            foreach (var account in accounts)
            {
                var balance = account.SelectToken("Summary.currentBalance")?.Value<string>();
                if (double.TryParse(balance, NumberStyles.Any, CultureInfo.InvariantCulture, out var bal))
                    totalBalance += bal;

                var odLimit = account.SelectToken("Summary.currentODLimit")?.Value<string>();
                if (double.TryParse(odLimit, NumberStyles.Any, CultureInfo.InvariantCulture, out var od))
                    totalOdLimit += od;

                foreach (var txn in account.SelectTokens("Transactions.Transaction[*]").OfType<JObject>())
                {
                    // The same consent session can return the same transaction under
                    // multiple FIPs — dedupe on txnId so aggregates aren't inflated.
                    var txnId = txn.Value<string>("txnId") ?? Guid.NewGuid().ToString();
                    if (!seenTxnIds.Add(txnId)) continue;

                    var date = txn.Value<string>("valueDate");
                    if (!DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDate))
                        continue;

                    double? runningBalance = null;
                    var balStr = txn.Value<string>("currentBalance");
                    if (double.TryParse(balStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var rb))
                        runningBalance = rb;

                    transactions.Add((
                        valueDate,
                        txn.Value<string>("type") ?? string.Empty,
                        txn.Value<string>("mode") ?? "OTHERS",
                        txn.Value<double?>("amount") ?? 0,
                        txn.Value<string>("narration") ?? string.Empty,
                        runningBalance));
                }
            }

            features.AaCurrentBalanceTotal = totalBalance;
            features.AaOdLimitTotal = totalOdLimit;
            features.AaTransactionCount = transactions.Count;
            if (transactions.Count == 0) return;

            // ── Monthly aggregates
            foreach (var group in transactions.GroupBy(t => t.Date.ToString("yyyyMM")).OrderBy(g => g.Key))
            {
                features.AaMonthlyCredits[group.Key] = group.Where(t => t.Type == "CREDIT").Sum(t => t.Amount);
                features.AaMonthlyDebits[group.Key] = group.Where(t => t.Type == "DEBIT").Sum(t => t.Amount);

                var balances = group.Where(t => t.Balance.HasValue).Select(t => t.Balance!.Value).ToList();
                if (balances.Count > 0)
                    features.AaMonthlyAvgBalance[group.Key] = Math.Round(balances.Average());
            }
            features.AaMonthsCovered = features.AaMonthlyCredits.Count;
            var months = Math.Max(1, features.AaMonthsCovered);

            var credits = features.AaMonthlyCredits.Values.ToArray();
            features.AaAvgMonthlyCredit = credits.Average();
            features.AaMonthlyAvgBankCredit = features.AaAvgMonthlyCredit;
            features.AaInflowVolatility = TrendMath.CoefficientOfVariation(credits);

            var avgDailyOutflow = features.AaMonthlyDebits.Values.DefaultIfEmpty(0).Average() / 30.44;
            features.AaDaysCashOnHand = avgDailyOutflow > 0 ? totalBalance / avgDailyOutflow : 365;

            // ── Balance behaviour: AMB, peak, overdraw, minimum-balance breaches
            var allBalances = transactions.Where(t => t.Balance.HasValue).Select(t => t.Balance!.Value).ToList();
            if (features.AaMonthlyAvgBalance.Count > 0)
                features.AaAvgMonthlyBalance = Math.Round(features.AaMonthlyAvgBalance.Values.Average());
            if (allBalances.Count > 0)
            {
                features.AaPeakBalance = allBalances.Max();
                features.AaOverdrawnTxnCount = allBalances.Count(b => b < 0);
                features.AaMinBalanceBreachCount = allBalances.Count(b => b >= 0 && b < MinBalanceThreshold);
            }

            // ── Return/discipline signals, separated by instrument
            features.AaBounceCount = transactions.Count(t => BouncePattern.IsMatch(t.Narration));
            features.AaChequeReturnCount = transactions.Count(t => ChequeReturnPattern.IsMatch(t.Narration));
            features.AaEcsNachReturnCount = transactions.Count(t => EcsNachReturnPattern.IsMatch(t.Narration));

            var emiDebits = transactions.Where(t => t.Type == "DEBIT" && EmiPattern.IsMatch(t.Narration)).ToList();
            features.AaEmiMonthlyOutflow = emiDebits.Count > 0 ? emiDebits.Sum(t => t.Amount) / months : 0;
            features.AaEmiToInflowRatio = features.AaAvgMonthlyCredit > 0
                ? Math.Clamp(features.AaEmiMonthlyOutflow / features.AaAvgMonthlyCredit, 0, 1) : 0;

            // ── Mode split (transaction value by channel: UPI / FT / IMPS / RTGS / CASH / …)
            foreach (var group in transactions.GroupBy(t => NormalizeMode(t.Mode, t.Narration)))
                features.AaModeSplitAmount[group.Key] = Math.Round(group.Sum(t => t.Amount));

            // ── Credit-side classification: cash deposits / salary / customer receipts
            var creditTxns = transactions.Where(t => t.Type == "CREDIT").ToList();
            var cashDeposits = creditTxns.Where(t => t.Mode.Equals("CASH", StringComparison.OrdinalIgnoreCase)
                                                  || CashDepositPattern.IsMatch(t.Narration)).ToList();
            var salaryCredits = creditTxns.Where(t => SalaryPattern.IsMatch(t.Narration)).ToList();
            features.AaCashDepositsMonthly = Math.Round(cashDeposits.Sum(t => t.Amount) / months);
            features.AaSalaryCreditsMonthly = Math.Round(salaryCredits.Sum(t => t.Amount) / months);

            // Customer receipts = credits that aren't salary, interest, or cash self-deposits
            var customerReceipts = creditTxns
                .Where(t => !SalaryPattern.IsMatch(t.Narration)
                         && !InterestPattern.IsMatch(t.Narration)
                         && !cashDeposits.Contains(t))
                .ToList();
            features.AaCustomerReceiptsMonthly = Math.Round(customerReceipts.Sum(t => t.Amount) / months);

            // ── Debit-side classification: supplier payments (business outflows via
            //    transfer channels, excluding EMIs/rent/utilities/statutory)
            var supplierPayments = transactions.Where(t =>
                    t.Type == "DEBIT"
                 && !NonBusinessDebitPattern.IsMatch(t.Narration)
                 && (SupplierPattern.IsMatch(t.Narration)
                     || t.Mode is "FT" or "NEFT" or "RTGS" or "IMPS"))
                .ToList();
            features.AaSupplierPaymentsMonthly = Math.Round(supplierPayments.Sum(t => t.Amount) / months);

            // ── UPI transaction quality
            features.AaUpiTxnShare = (double)transactions.Count(t => t.Mode == "UPI") / transactions.Count;
            if (creditTxns.Count > 0)
            {
                var payers = creditTxns
                    .Select(t => NormalizeCounterparty(t.Narration))
                    .Where(p => p.Length > 0)
                    .ToList();
                var distinct = payers.Distinct(StringComparer.OrdinalIgnoreCase).Count();
                features.AaUpiCounterpartyCount = distinct;
                features.AaRepeatPayerRatio = payers.Count > 0 ? 1.0 - (double)distinct / payers.Count : 0;
            }
        }

        /// <summary>Channel bucket from the AA mode field, refined by narration where mode is generic.</summary>
        private static string NormalizeMode(string mode, string narration)
        {
            var m = mode.ToUpperInvariant();
            if (m is "UPI" or "CASH" or "IMPS" or "RTGS" or "NEFT" or "CARD" or "ATM") return m;
            if (narration.Contains("IMPS", StringComparison.OrdinalIgnoreCase)) return "IMPS";
            if (narration.Contains("RTGS", StringComparison.OrdinalIgnoreCase)) return "RTGS";
            if (narration.Contains("NEFT", StringComparison.OrdinalIgnoreCase)) return "NEFT";
            if (CashDepositPattern.IsMatch(narration)) return "CASH";
            return m == "FT" ? "FT" : "OTHERS";
        }

        /// <summary>Rough counterparty key from a narration like "NEFT/CLIENT PAYMENT" or "UPI/name".</summary>
        private static string NormalizeCounterparty(string narration)
        {
            var parts = narration.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Length > 1 ? parts[^1].ToUpperInvariant() : narration.Trim().ToUpperInvariant();
        }
    }
}
