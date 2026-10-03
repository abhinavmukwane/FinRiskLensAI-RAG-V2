using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Core.Interfaces.IServices.Cases
{
    /// <summary>Case intake and retrieval for the bank portal's Cases screens.</summary>
    public interface ICaseService
    {
        /// <summary>Validates, fingerprints (SHA-256 + PDF metadata) and stores the files, then records the case.</summary>
        Task<CaseIntakeResult> CreateAsync(NewCaseRequest request, IReadOnlyList<CaseUpload> files, string actor,
                                           CancellationToken ct = default);
        Task<IReadOnlyList<CaseListItem>> ListAsync(CancellationToken ct = default);
        Task<CaseDetailViewModel?> GetAsync(int loanApplicationId, CancellationToken ct = default);
        Task<CaseFile?> OpenDocumentAsync(int loanApplicationId, int caseDocumentId, CancellationToken ct = default);

        /// <summary>
        /// Stores the extractor's output for one document of the application. False when the
        /// document does not belong to it. Does not run the checks — call <see cref="RunChecksAsync"/>
        /// once the case's documents are in.
        /// </summary>
        Task<bool> ApplyExtractionAsync(int loanApplicationId, int caseDocumentId, ExtractedDocument extracted,
                                        string actor, CancellationToken ct = default);

        /// <summary>Runs the deterministic checks over whatever has been extracted so far. Safe to repeat.</summary>
        Task RunChecksAsync(int loanApplicationId, CancellationToken ct = default);

        Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status, string reviewedBy,
                                      string? note, CancellationToken ct = default);
    }
}
