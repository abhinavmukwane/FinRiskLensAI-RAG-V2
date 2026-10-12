using System.Security.Cryptography;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
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

        public CaseService(ICaseRepository cases, ICaseDocumentStore store, FraudOptions options)
        {
            _cases = cases;
            _store = store;
            _options = options;
        }

        public async Task<CaseIntakeResult> CreateAsync(NewCaseRequest request, IReadOnlyList<CaseUpload> files,
                                                        string actor, CancellationToken ct = default)
        {
            if (files.Count == 0) return new(null, "Attach at least one document.");
            if (files.Count > MaxFiles) return new(null, $"At most {MaxFiles} documents per case.");

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
                UpdatedBy = actor
            };

            // Validate every file before storing any, so a bad fourth file does not
            // leave three orphans behind.
            var accepted = new List<(CaseUpload File, string ContentType, string Extension)>();
            foreach (var f in files)
            {
                if (f.Content.Length == 0) return new(null, $"{f.FileName} is empty.");
                if (f.Content.Length > MaxFileBytes) return new(null, $"{f.FileName} is over {MaxFileBytes / 1024 / 1024} MB.");
                var type = DocumentInspector.Sniff(f.Content);
                if (type == null) return new(null, $"{f.FileName} is not a PDF, PNG or JPEG.");
                accepted.Add((f, type.Value.ContentType, type.Value.Extension));
            }

            var seen = new HashSet<string>();
            foreach (var (file, contentType, extension) in accepted)
            {
                var sha = SHA256.HashData(file.Content);
                var hex = Convert.ToHexString(sha).ToLowerInvariant();
                if (!seen.Add(hex)) continue;   // the same file attached twice

                var path = $"cases/{hex}{extension}";
                await _store.PutAsync(path, file.Content, contentType, ct);

                var meta = contentType == "application/pdf" ? DocumentInspector.ReadPdf(file.Content) : null;
                application.Documents.Add(new CaseDocument
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

            var id = await _cases.AddAsync(application, ct);

            // Metadata checks need nothing but the upload, so the case opens with them run
            await RunChecksAsync(id, ct);
            return new(id, null);
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
            var application = await _cases.GetAsync(loanApplicationId, ct);
            if (application == null) return;

            var (rows, pages, fields) = await _cases.GetExtractedAsync(loanApplicationId, ct);
            var findings = DeterministicChecks.Run(new CaseData(application, rows, pages, fields), _options);
            foreach (var f in findings) { f.CreatedBy = "DETERMINISTIC_CHECKS"; f.UpdatedBy = f.CreatedBy; }

            await _cases.ReplaceFindingsAsync(loanApplicationId, FindingSource.Deterministic, findings, ct);
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
