using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
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
        private readonly IBlobAnalysisService? _facts;

        /// <param name="sources">With <paramref name="aa"/>, the bank's own record of linked accounts; without them check 9 does not run.</param>
        public CaseService(ICaseRepository cases, ICaseDocumentStore store, FraudOptions options,
                           IMsmeDataStore? sources = null, IAaStatementAnalysisService? aa = null,
                           IBlobAnalysisService? facts = null)
        {
            _cases = cases;
            _store = store;
            _options = options;
            _sources = sources;
            _aa = aa;
            _facts = facts;
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
                id = await _cases.AddAsync(NewAssessment(uan, enterpriseName, actor, documents), ct);
            }
            else
            {
                id = existing.LoanApplicationID;
                await _cases.AddDocumentsAsync(id, documents, ct);
            }

            await RunChecksAsync(id, ct);
            return new(id, null);
        }

        public async Task<int> EnsureAssessmentAsync(string uan, string? enterpriseName, string actor, CancellationToken ct = default)
        {
            uan = uan.Trim().ToUpperInvariant();
            SourceLinkCache.TryRemove(uan, out _);   // a source just arrived: its directors or buyers may have changed
            return await _cases.FindByUanAsync(uan, ct) ?? await _cases.AddAsync(NewAssessment(uan, enterpriseName, actor, new()), ct);
        }

        /// <summary>A customer's one assessment: keyed by the Udyam number, the business name as the declared employer.</summary>
        private static LoanApplication NewAssessment(string uan, string? enterpriseName, string actor, List<CaseDocument> documents)
        {
            var name = string.IsNullOrWhiteSpace(enterpriseName) ? null : enterpriseName.Trim();
            return new LoanApplication
            {
                ApplicantRef = uan,
                ProductType = CaseProduct.SmeLoan,
                Uan = uan,
                DeclaredEmployer = name?.Length > 200 ? name[..200] : name,
                SubmittedAt = DateTime.UtcNow,
                CreatedBy = actor,
                UpdatedBy = actor,
                Documents = documents
            };
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

        private async Task<IReadOnlyList<Ring>> ListRingsAsync(CancellationToken ct = default)
        {
            var network = await _cases.GetNetworkAsync(ct);
            if (_sources == null) return NetworkLens.Find(network);

            // Registered customers: add what their sources say — directors, own GSTIN, the buyers they invoice
            var enriched = await Task.WhenAll(network.Select(async a =>
            {
                if (a.Uan == null || a.Entities == null) return a;
                var s = await SourceLinksAsync(a.Uan, ct);
                return a with { Entities = a.Entities with { Gstin = a.Entities.Gstin ?? s.Gstin, Directors = s.Directors, InvoicedGstins = s.InvoicedGstins } };
            }));
            return NetworkLens.Find(enriched);
        }

        private sealed record SourceLinks(string? Gstin, IReadOnlyList<Director> Directors, IReadOnlyList<string> InvoicedGstins);

        // ponytail: each customer's source links are read once and kept 10 minutes (dropped when a source arrives).
        // Fine to a few hundred customers; past that, write them to an application_entities table when sources arrive.
        private static readonly ConcurrentDictionary<string, (DateTime At, SourceLinks Links)> SourceLinkCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim SourceReads = new(8);

        /// <summary>Active MCA directors, and from the GSTR-1 B2B returns the business's own GSTIN and the buyers it invoices.</summary>
        private async Task<SourceLinks> SourceLinksAsync(string uan, CancellationToken ct)
        {
            if (SourceLinkCache.TryGetValue(uan, out var cached) && cached.At > DateTime.UtcNow.AddMinutes(-10)) return cached.Links;

            var files = await _sources!.ListFilesAsync(uan, ct);
            async Task<JsonDocument?> ReadAsync(string file)
            {
                await SourceReads.WaitAsync(ct);
                try { return await _sources.DownloadAsync(uan, file, ct) is { } json ? JsonDocument.Parse(json) : null; }
                catch (JsonException) { return null; }   // a malformed payload links nothing; it is not this check's to report
                finally { SourceReads.Release(); }
            }

            var directors = new List<Director>();
            if (files.Contains(MsmeDataFiles.Mca, StringComparer.OrdinalIgnoreCase) && await ReadAsync(MsmeDataFiles.Mca) is { } mca)
                using (mca)
                    foreach (var d in Items(At(mca.RootElement, "message", "details", "directors")))
                        if (Text(d, "din_number") is { Length: > 0 } din && string.IsNullOrWhiteSpace(Text(d, "end_date")))
                            directors.Add(new Director(din.Trim(), Text(d, "director_name")?.Trim() ?? din));

            string? gstin = null;
            var buyers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var invoices = await Task.WhenAll(files.Where(f => f.StartsWith("gstr1_b2b_", StringComparison.OrdinalIgnoreCase)).Select(ReadAsync));
            foreach (var doc in invoices.OfType<JsonDocument>())
                using (doc)
                {
                    var data = At(doc.RootElement, "response", "message", "data");
                    gstin ??= Text(data, "gstin");
                    foreach (var invoice in Items(At(data, "b2b")))
                        if (Text(invoice, "ctin") is { Length: > 0 } buyer) buyers.Add(buyer.Trim().ToUpperInvariant());
                }

            var links = new SourceLinks(gstin, directors, buyers.ToList());
            SourceLinkCache[uan] = (DateTime.UtcNow, links);
            return links;
        }

        private static JsonElement At(JsonElement e, params string[] path)
        {
            foreach (var p in path)
                if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(p, out e)) return default;
            return e;
        }
        private static IEnumerable<JsonElement> Items(JsonElement e) => e.ValueKind == JsonValueKind.Array ? e.EnumerateArray() : Enumerable.Empty<JsonElement>();
        private static string? Text(JsonElement e, string name)
            => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>This application's checks and its ring finding; returns the other members of its ring.</summary>
        private async Task<IReadOnlyList<int>> RunChecksOnceAsync(int loanApplicationId, CancellationToken ct)
        {
            var application = await _cases.GetAsync(loanApplicationId, ct);
            if (application == null) return Array.Empty<int>();

            var (rows, pages, fields) = await _cases.GetExtractedAsync(loanApplicationId, ct);
            // The bank's own record: compared with any uploaded statement, and read for its own risk patterns
            var bank = await BankAnalysisAsync(application.Uan, ct);
            await _cases.SetBankDataAsync(loanApplicationId, bank?.HasData == true ? DateTime.UtcNow : null, ct);
            var findings = DeterministicChecks.Run(new CaseData(application, rows, pages, fields, rows.Count > 0 ? bank?.Transactions : null), _options);
            findings.AddRange(SourceChecks.BankFlags(bank, loanApplicationId));

            if (!string.IsNullOrWhiteSpace(application.Uan))
            {
                // Turnover check (Task T3): GST vs ITR vs bank credits
                if (_facts != null)
                {
                    try
                    {
                        var msmeFacts = await _facts.GetFactsAsync(application.Uan, ct);
                        if (msmeFacts?.Features != null)
                        {
                            var turnoverFinding = SourceChecks.Turnover(msmeFacts.Features, _options, loanApplicationId);
                            if (turnoverFinding != null) findings.Add(turnoverFinding);
                        }
                    }
                    catch
                    {
                        // source facts not yet computed or unavailable
                    }
                }

                // Identity check (Task T4): PAN, GSTIN, business name across sources, MCA status
                if (_sources != null)
                {
                    try
                    {
                        var files = await _sources.ListFilesAsync(application.Uan, ct);
                        string? udyamJson = files.Contains(MsmeDataFiles.Udyam, StringComparer.OrdinalIgnoreCase)
                            ? await _sources.DownloadAsync(application.Uan, MsmeDataFiles.Udyam, ct) : null;
                        string? gstTaxpayerJson = files.Contains(MsmeDataFiles.GstTaxpayer, StringComparer.OrdinalIgnoreCase)
                            ? await _sources.DownloadAsync(application.Uan, MsmeDataFiles.GstTaxpayer, ct) : null;
                        string? itrJson = files.Contains(MsmeDataFiles.Itr, StringComparer.OrdinalIgnoreCase)
                            ? await _sources.DownloadAsync(application.Uan, MsmeDataFiles.Itr, ct) : null;
                        string? mcaJson = files.Contains(MsmeDataFiles.Mca, StringComparer.OrdinalIgnoreCase)
                            ? await _sources.DownloadAsync(application.Uan, MsmeDataFiles.Mca, ct) : null;

                        var idFindings = SourceChecks.Identity(udyamJson, gstTaxpayerJson, itrJson, mcaJson, loanApplicationId);
                        findings.AddRange(idFindings);
                    }
                    catch
                    {
                        // sources not yet uploaded or unavailable
                    }
                }
            }

            var ring = (await ListRingsAsync(ct)).FirstOrDefault(r => r.Members.Any(m => m.LoanApplicationID == loanApplicationId));
            if (ring != null) findings.Add(NetworkLens.FindingFor(ring, loanApplicationId));

            foreach (var f in findings) { f.CreatedBy = "DETERMINISTIC_CHECKS"; f.UpdatedBy = f.CreatedBy; }
            await _cases.ReplaceFindingsAsync(loanApplicationId, FindingSource.Deterministic, findings, ct);

            return ring?.Members.Select(m => m.LoanApplicationID).Where(id => id != loanApplicationId).ToList() ?? (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>The Account Aggregator analysis for the linked Udyam number; null when no bank data was shared.</summary>
        private async Task<AaAnalysisResult?> BankAnalysisAsync(string? uan, CancellationToken ct)
        {
            if (uan == null || _sources == null || _aa == null) return null;
            var json = await _sources.DownloadAsync(uan, MsmeDataFiles.Aa, ct);
            return json == null ? null : _aa.Analyze(json);
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
