using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Core.Interfaces.IRepositories.Admin
{
    public interface IBankAdminRepository
    {
        /// <summary>Active bank user by login id, or null. Includes the password hash — service-layer use only.</summary>
        Task<AdmBankLogin?> GetByUserIdAsync(string userId, CancellationToken ct = default);

        Task RecordLoginSuccessAsync(int admBankLoginId, CancellationToken ct = default);

        Task RecordLoginFailureAsync(int admBankLoginId, CancellationToken ct = default);

        /// <summary>Insert-or-update the score summary row for a UAN.</summary>
        Task UpsertScoreSummaryAsync(MsmeScoreSummary summary, CancellationToken ct = default);

        /// <summary>Filtered, sorted, paged list of onboarded MSMEs with their scores.</summary>
        Task<BankCustomerPage> GetCustomersAsync(BankCustomerQuery query, CancellationToken ct = default);

        /// <summary>One customer's profile + score row, or null when the UAN is unknown.</summary>
        Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default);

        /// <summary>Portfolio aggregates for the dashboard KPIs and charts.</summary>
        Task<BankPortfolioStats> GetPortfolioStatsAsync(CancellationToken ct = default);

        /// <summary>Distinct states present in the portfolio, for the filter dropdown.</summary>
        Task<List<string>> GetStatesAsync(CancellationToken ct = default);

        /// <summary>Every onboarded UAN — drives the score backfill.</summary>
        Task<List<string>> GetAllUansAsync(CancellationToken ct = default);
    }
}
