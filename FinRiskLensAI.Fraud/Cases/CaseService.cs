using System.Security.Cryptography;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Fraud.Checks;

namespace FinRiskLensAI.Fraud.Cases
{
    /// <summary>
    /// Step ① of the case pipeline (Doc/10_FRAUD_LENS_BUILD.md §4): store, hash and
    /// read metadata. Extraction (Gemini) picks documents up from ExtractStatus.Pending
    /// once the GCP project exists.
    /// </summary>
    public class CaseService : ICaseService
    {
        public const int MaxFiles = 8;
        public const int MaxFileBytes = 15 * 1024 * 1024;

        private readonly ICaseRepository _cases;
        private readonly ICaseDocumentStore _store;
        private readonly FraudOptions _options;
        private readonly IMsmeDataStore? _sources;
        private readonly IAaStatementAnalysisService? _aa;

        /// <param name="sources">With <paramref name="aa"/>, the bank's own record of linked accounts; without them check 9 does not run.</param>
        public CaseService(ICaseRepository cases, ICaseDocumentStore store, FraudOptions options,
                           IMsmeDataStore? sources = null, IAaStatementAnalysisService? aa = null)
        {
            _cases = cases;
            _store = store;
            _options = options;
            _sources = sources;
            _aa = aa;
        }

        public async Task<CaseIntakeResult> CreateAsync(NewCaseRequest request, IReadOnlyList<CaseUpload> files,
                                                        string actor, CancellationToken ct = default)
        {
            var (documents, error) = await StoreAsync(files, actor, new HashSet<string>(), ct);
            if (error != null) return new(null, error);

            var application = new LoanApplication
            {
                ApplicantRef = request.ApplicantRef.Trim(),
                ProductType = request.ProductType,
                Uan = string.IsNullOrWhiteSpace(request.Uan) ? null : request.Uan.Trim().ToUpperInvariant(),
                DeclaredIncome = request.DeclaredIncome,
                DeclaredEmployer = string.IsNullOrWhiteSpace(request.DeclaredEmployer) ? null : request.DeclaredEmployer.Trim(),
                DeclaredEmis = request.DeclaredEmis,
                SubmittedAt = DateTime.UtcNow,
                CreatedBy = actor,
                UpdatedBy = actor,
                Documents = documents
            };
            var id = await _cases.AddAsync(application, ct);

            // Metadata checks need nothing but the upload, so the case opens with them run
            await RunChecksAsync(id, ct);
            return new(id, null);
        }

        public async Task<CaseIntakeResult> AddCustomerDocumentsAsync(string uan, string? enterpriseName, IReadOnlyList<CaseUpload> files,
                                                                      string actor, CancellationToken ct = default)
        {
            uan = uan.Trim().ToUpperInvariant();
            var existingId = await _cases.FindByUanAsync(uan, ct);
            var existing = existingId == null ? null : await _cases.GetAsync(existingId.Value, ct);
            var attached = existing?.Documents.Select(d => Convert.ToHexString(d.Sha256).ToLowerInvariant()).ToHashSet() ?? new HashSet<string>();

            var (documents, error) = await StoreAsync(files, actor, attached, ct);
            if (error != null) return new(null, error);
            if (documents.Count == 0) return new(existingId, "These files have already been shared.");

            int id;
            if (existing == null)
            {
                // The customer's one assessment, opened by their first upload
                var name = string.IsNullOrWhiteSpace(enterpriseName) ? null : enterpriseName.Trim();
                id = await _cases.AddAsync(new LoanApplication
                {
                    ApplicantRef = uan,
                    ProductType = CaseProduct.SmeLoan,
                    Uan = uan,
                    DeclaredEmployer = name?.Length > 200 ? name[..200] : name,
                    SubmittedAt = DateTime.UtcNow,
                    CreatedBy = actor,
                    UpdatedBy = actor,
                    Documents = documents
                }, ct);
            }
            else
            {
                id = existing.LoanApplicationID;
                await _cases.AddDocumentsAsync(id, documents, ct);
            }

            await RunChecksAsync(id, ct);
            return new(id, null);
        }

        public async Task<CaseDetailViewModel?> GetByUanAsync(string uan, CancellationToken ct = default)
            => await _cases.FindByUanAsync(uan.Trim().ToUpperInvariant(), ct) is int id ? await GetAsync(id, ct) : null;

        public Task<IReadOnlyDictionary<string, RiskSummary>> GetRiskSummariesAsync(CancellationToken ct = default)
            => _cases.GetRiskSummariesAsync(ct);

        /// <summary>
        /// Validates every file before storing any — so a bad fourth file does not leave three orphans behind —
        /// then stores each content-addressed and reads its PDF metadata. Files in <paramref name="skip"/>
        /// (lower-case SHA-256 hex) and repeats within the upload are left out.
        /// </summary>
        private async Task<(List<CaseDocument> Documents, string? Error)> StoreAsync(IReadOnlyList<CaseUpload> files, string actor,
                                                                                  HashSet<string> skip, CancellationToken ct)
        {
            if (files.Count == 0) return (new(), "Attach at least one document.");
            if (files.Count > MaxFiles) return (new(), $"At most {MaxFiles} documents at a time.");

            var accepted = new List<(CaseUpload File, string ContentType, string Extension)>();
            foreach (var f in files)
            {
                if (f.Content.Length == 0) return (new(), $"{f.FileName} is empty.");
                if (f.Content.Length > MaxFileBytes) return (new(), $"{f.FileName} is over {MaxFileBytes / 1024 / 1024} MB.");
                var type = DocumentInspector.Sniff(f.Content);
                if (type == null) return (new(), $"{f.FileName} is not a PDF, PNG or JPEG.");
                accepted.Add((f, type.Value.ContentType, type.Value.Extension));
            }

            var documents = new List<CaseDocument>();
            foreach (var (file, contentType, extension) in accepted)
            {
                var sha = SHA256.HashData(file.Content);
                var hex = Convert.ToHexString(sha).ToLowerInvariant();
                if (!skip.Add(hex)) continue;   // already attached, or the same file twice in this upload

                var path = $"cases/{hex}{extension}";
                await _store.PutAsync(path, file.Content, contentType, ct);

                var meta = contentType == "application/pdf" ? DocumentInspector.ReadPdf(file.Content) : null;
                documents.Add(new CaseDocument
                {
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = contentType,
                    SizeBytes = file.Content.Length,
                    StoragePath = path,
                    Sha256 = sha,
                    PageCount = contentType == "application/pdf" ? meta!.PageCount : 1,
                    PdfProducer = meta?.Producer,
                    PdfCreator = meta?.Creator,
                    PdfCreatedAt = meta?.CreatedAt,
                    PdfModifiedAt = meta?.ModifiedAt,
                    FontSet = meta?.FontSet,
                    CreatedBy = actor,
                    UpdatedBy = actor
                });
            }
            return (documents, null);
        }

        public Task<IReadOnlyList<CaseListItem>> ListAsync(CancellationToken ct = default) => _cases.ListAsync(ct);

        public async Task<CaseDetailViewModel?> GetAsync(int loanApplicationId, CancellationToken ct = default)
        {
            var application = await _cases.GetAsync(loanApplicationId, ct);
            if (application == null) return null;

            return new CaseDetailViewModel
            {
                Application = application,
                SharedWith = await _cases.FindSharedFilesAsync(loanApplicationId, ct),
                Findings = await _cases.GetFindingsAsync(loanApplicationId, ct),
                RejectedClaims = await _cases.GetLatestDropsAsync(loanApplicationId, ct)
            };
        }

        public async Task<CaseFile?> OpenDocumentAsync(int loanApplicationId, int caseDocumentId, CancellationToken ct = default)
        {
            var doc = await _cases.GetDocumentAsync(loanApplicationId, caseDocumentId, ct);
            if (doc == null) return null;

            var stream = await _store.OpenReadAsync(doc.StoragePath, ct);
            return stream == null ? null : new CaseFile(stream, doc.ContentType, doc.FileName);
        }

        public async Task<bool> ApplyExtractionAsync(int loanApplicationId, int caseDocumentId, ExtractedDocument extracted,
                                                     string actor, CancellationToken ct = default)
        {
            if (await _cases.GetDocumentAsync(loanApplicationId, caseDocumentId, ct) == null) return false;
            await _cases.SaveExtractionAsync(caseDocumentId, extracted, actor, ct);
            return true;
        }

        public async Task RunChecksAsync(int loanApplicationId, CancellationToken ct = default)
        {
            // Joining a ring changes it for everyone already in it, so their findings are refreshed too
            foreach (var other in await RunChecksOnceAsync(loanApplicationId, ct))
                await RunChecksOnceAsync(other, ct);
        }

        public async Task<IReadOnlyList<Ring>> ListRingsAsync(CancellationToken ct = default)
            => NetworkLens.Find(await _cases.GetNetworkAsync(ct));

        /// <summary>This application's checks and its ring finding; returns the other members of its ring.</summary>
        private async Task<IReadOnlyList<int>> RunChecksOnceAsync(int loanApplicationId, CancellationToken ct)
        {
            var application = await _cases.GetAsync(loanApplicationId, ct);
            if (application == null) return Array.Empty<int>();

            var (rows, pages, fields) = await _cases.GetExtractedAsync(loanApplicationId, ct);
            var bankRecord = rows.Count > 0 ? await BankRecordAsync(application.Uan, ct) : null;
            var findings = DeterministicChecks.Run(new CaseData(application, rows, pages, fields, bankRecord), _options);

            var ring = (await ListRingsAsync(ct)).FirstOrDefault(r => r.Members.Any(m => m.LoanApplicationID == loanApplicationId));
            if (ring != null) findings.Add(NetworkLens.FindingFor(ring, loanApplicationId));

            foreach (var f in findings) { f.CreatedBy = "DETERMINISTIC_CHECKS"; f.UpdatedBy = f.CreatedBy; }
            await _cases.ReplaceFindingsAsync(loanApplicationId, FindingSource.Deterministic, findings, ct);

            return ring?.Members.Select(m => m.LoanApplicationID).Where(id => id != loanApplicationId).ToList() ?? (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>The bank's transactions for the linked Udyam number, as fetched through Account Aggregator; null when there are none.</summary>
        private async Task<IReadOnlyList<AaTransaction>?> BankRecordAsync(string? uan, CancellationToken ct)
        {
            if (uan == null || _sources == null || _aa == null) return null;
            var json = await _sources.DownloadAsync(uan, MsmeDataFiles.Aa, ct);
            return json == null ? null : _aa.Analyze(json).Transactions;
        }

        public async Task<bool> MarkProcessingAsync(int loanApplicationId, CancellationToken ct = default)
        {
            if (await _cases.GetAsync(loanApplicationId, ct) == null) return false;
            await _cases.SetStatusAsync(loanApplicationId, CaseStatus.Processing, ct);
            return true;
        }

        public Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status, string reviewedBy,
                                             string? note, CancellationToken ct = default)
            => _cases.ReviewFindingAsync(loanApplicationId, findingId, status, reviewedBy, note, ct);
    }
}
