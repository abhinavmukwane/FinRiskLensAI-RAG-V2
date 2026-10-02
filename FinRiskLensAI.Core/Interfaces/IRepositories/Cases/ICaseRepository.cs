using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Core.Interfaces.IRepositories.Cases
{
    public interface ICaseRepository
    {
        /// <summary>Saves the application with its documents in one transaction.</summary>
        Task<int> AddAsync(LoanApplication application, CancellationToken ct = default);
        Task<IReadOnlyList<CaseListItem>> ListAsync(CancellationToken ct = default);
        Task<LoanApplication?> GetAsync(int loanApplicationId, CancellationToken ct = default);
        Task<CaseDocument?> GetDocumentAsync(int loanApplicationId, int caseDocumentId, CancellationToken ct = default);

        /// <summary>For each document of this application, the other applications holding the same bytes.</summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindSharedFilesAsync(int loanApplicationId, CancellationToken ct = default);
    }
}
