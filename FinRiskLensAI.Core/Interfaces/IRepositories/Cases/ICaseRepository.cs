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

        /// <summary>What the extractor wrote for this application's documents.</summary>
        Task<(IReadOnlyList<StatementRow> Rows, IReadOnlyList<PageSummary> Pages, IReadOnlyList<DocField> Fields)>
            GetExtractedAsync(int loanApplicationId, CancellationToken ct = default);

        Task<IReadOnlyList<Finding>> GetFindingsAsync(int loanApplicationId, CancellationToken ct = default);

        /// <summary>
        /// Replaces everything previously extracted for the document with <paramref name="extracted"/>
        /// and marks it Done. Rows are numbered in reading order across pages.
        /// </summary>
        Task SaveExtractionAsync(int caseDocumentId, ExtractedDocument extracted, string actor, CancellationToken ct = default);

        /// <summary>
        /// Replaces the application's open deterministic findings with <paramref name="fresh"/>.
        /// Reviewed findings are never touched, and a fresh finding with the same key as a
        /// reviewed one is dropped — a re-run must not resurrect what an investigator dismissed.
        /// Moves a New application to Review when anything is left unreviewed.
        /// </summary>
        Task ReplaceDeterministicFindingsAsync(int loanApplicationId, IReadOnlyList<Finding> fresh, CancellationToken ct = default);

        Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status, string reviewedBy,
                                      string? note, CancellationToken ct = default);
    }
}
