using FinRiskLensAI.Core.Models.LoanCase;

namespace FinRiskLensAI.Core.Interfaces.IRepositories.Admin
{
    public interface ILoanCasePushRepository
    {
        /// <summary>Appends one push record. Never updates.</summary>
        Task<int> AddAsync(LoanCasePush push, CancellationToken ct = default);

        Task<LoanCasePush?> GetAsync(int loanCasePushId, CancellationToken ct = default);

        /// <summary>Latest push per channel for one UAN — the Customer 360 chips.</summary>
        Task<IReadOnlyList<LoanCasePushSummary>> GetLatestByChannelAsync(string uan, CancellationToken ct = default);

        /// <summary>Latest push (any channel) per UAN, for the customers list badge.</summary>
        Task<IReadOnlyDictionary<string, LoanCasePushSummary>> GetLatestForAsync(IEnumerable<string> uans, CancellationToken ct = default);
    }
}
