using FinRiskLensAI.Core.Models;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Core.Interfaces.IServices.Admin
{
    public interface IBankAdminService
    {
        /// <summary>
        /// Verifies user id + password against ADM_BankLogin. On success returns the
        /// session snapshot (never the password hash) and stamps LastLoginAt.
        /// </summary>
        Task<ResultModel<BankUserSessionModel>> ValidateLoginAsync(string userId, string password, CancellationToken ct = default);

        /// <summary>Produces an Identity V3 PBKDF2 hash — used when provisioning bank users.</summary>
        string HashPassword(string password);

        /// <summary>Projects a computed analysis result into the SQL score-summary index.</summary>
        Task SaveScoreSummaryAsync(string uan, RiskAnalysisResult result, CancellationToken ct = default);

        /// <summary>The score trend series for a UAN, oldest first.</summary>
        Task<IReadOnlyList<ScoreHistoryPoint>> GetScoreHistoryAsync(string uan, int take = 50, CancellationToken ct = default);

        /// <summary>Filtered, sorted, paged list of onboarded MSMEs with their scores.</summary>
        Task<BankCustomerPage> GetCustomersAsync(BankCustomerQuery query, CancellationToken ct = default);

        /// <summary>One customer's profile + score row, or null when the UAN is unknown.</summary>
        Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default);

        /// <summary>Portfolio aggregates for the dashboard KPIs and charts.</summary>
        Task<BankPortfolioStats> GetPortfolioStatsAsync(CancellationToken ct = default);

        /// <summary>Distinct states present in the portfolio, for the filter dropdown.</summary>
        Task<List<string>> GetStatesAsync(CancellationToken ct = default);
    }
}
