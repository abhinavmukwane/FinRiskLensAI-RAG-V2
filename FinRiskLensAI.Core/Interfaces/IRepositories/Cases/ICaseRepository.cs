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

        /// <summary>Every application with its documents and its salary credits — the network lens's input.</summary>
        Task<IReadOnlyList<NetworkApplication>> GetNetworkAsync(CancellationToken ct = default);

        /// <summary>The customer's assessment — the latest application for this Udyam number — or null.</summary>
        Task<int?> FindByUanAsync(string uan, CancellationToken ct = default);

        /// <summary>Stores a new credit decision (append-only) and returns its id.</summary>
        Task<int> AddCreditDecisionAsync(CreditDecision decision, CancellationToken ct = default);

        /// <summary>The latest credit decision for an assessment, or null.</summary>
        Task<CreditDecision?> GetLatestCreditDecisionAsync(int loanApplicationId, CancellationToken ct = default);

        /// <summary>Records the officer's acceptance. False when the decision is not this assessment's.</summary>
        Task<bool> AcceptCreditDecisionAsync(int creditDecisionId, int loanApplicationId, decimal amount, string acceptedBy,
                                             CancellationToken ct = default);

        /// <summary>Records whether the last run read the bank's own record (Account Aggregator).</summary>
        Task SetBankDataAsync(int loanApplicationId, DateTime? at, CancellationToken ct = default);

        /// <summary>Attaches more documents to an existing application.</summary>
        Task AddDocumentsAsync(int loanApplicationId, IReadOnlyList<CaseDocument> documents, CancellationToken ct = default);

        /// <summary>Per Udyam number, the latest assessment's open findings by severity and whether it is still being worked on.</summary>
        Task<IReadOnlyDictionary<string, RiskSummary>> GetRiskSummariesAsync(CancellationToken ct = default);

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
        /// Replaces the application's open findings from <paramref name="source"/> with <paramref name="fresh"/>.
        /// Reviewed findings are never touched, and a fresh finding with the same key as a
        /// reviewed one is dropped — a re-run must not resurrect what an investigator dismissed.
        /// Moves a New application to Review when anything is left unreviewed.
        /// </summary>
        Task ReplaceFindingsAsync(int loanApplicationId, FindingSource source, IReadOnlyList<Finding> fresh, CancellationToken ct = default);

        /// <summary>Records an investigation run: its rejected claims and when and by which model it ran.</summary>
        Task RecordInvestigationAsync(int loanApplicationId, DateTime at, string model, IReadOnlyList<AiDropLog> drops,
                                      CancellationToken ct = default);

        /// <summary>The claims the grounding verifier rejected in the application's most recent investigation.</summary>
        Task<IReadOnlyList<AiDropLog>> GetLatestDropsAsync(int loanApplicationId, CancellationToken ct = default);

        Task SetStatusAsync(int loanApplicationId, CaseStatus status, CancellationToken ct = default);

        Task MarkExtractionFailedAsync(int caseDocumentId, string error, CancellationToken ct = default);

        /// <summary>Applications with at least one document still at Pending — what a restart left behind.</summary>
        Task<IReadOnlyList<int>> GetAwaitingExtractionAsync(CancellationToken ct = default);

        Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status, string reviewedBy,
                                      string? note, CancellationToken ct = default);
    }
}
