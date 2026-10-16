using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Fraud.Checks
{
    /// <summary>
    /// Checks on what the customer shared from its sources — the bank's Account Aggregator record, GST,
    /// ITR, the registries — rather than on an uploaded document (Doc/12_CUSTOMER_RISK_LENS.md §4).
    /// Pure functions, one finding per pattern per customer.
    /// </summary>
    public static class SourceChecks
    {
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

        private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
