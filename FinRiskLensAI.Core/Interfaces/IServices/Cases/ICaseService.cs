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

        /// <summary>Every fraud ring across all applications, largest first.</summary>
        Task<IReadOnlyList<Ring>> ListRingsAsync(CancellationToken ct = default);

        /// <summary>
        /// The customer's own uploads: attached to their one assessment (created on the first upload), stored and
        /// checked like any upload. Files already attached are skipped. The caller queues the pipeline.
        /// </summary>
        Task<CaseIntakeResult> AddCustomerDocumentsAsync(string uan, string? enterpriseName, IReadOnlyList<CaseUpload> files,
                                                         string actor, CancellationToken ct = default);

        /// <summary>
        /// The customer's assessment, opened empty if they have none yet — called when a source (bank data,
        /// GST, ITR, MCA) arrives, so the customer is assessed before any upload. The caller queues the pipeline.
        /// </summary>
        Task<int> EnsureAssessmentAsync(string uan, string? enterpriseName, string actor, CancellationToken ct = default);

        /// <summary>The customer's assessment with its findings, or null before they have shared anything to assess.</summary>
        Task<CaseDetailViewModel?> GetByUanAsync(string uan, CancellationToken ct = default);

        /// <summary>Risk at a glance for every customer with an assessment, keyed by Udyam number.</summary>
        Task<IReadOnlyDictionary<string, RiskSummary>> GetRiskSummariesAsync(CancellationToken ct = default);

        /// <summary>Shows the case as Processing before the pipeline picks it up. False when it does not exist.</summary>
        Task<bool> MarkProcessingAsync(int loanApplicationId, CancellationToken ct = default);

        Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status, string reviewedBy,
                                      string? note, CancellationToken ct = default);
    }
}
