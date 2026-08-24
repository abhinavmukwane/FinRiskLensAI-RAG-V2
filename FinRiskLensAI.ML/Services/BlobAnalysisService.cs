using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.Admin;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.ML.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// Blob-folder analysis flow: source payloads are uploaded file-by-file into the
    /// MSME's folder (named by Udyam number); when everything the manifest expects is
    /// present, this service downloads one file at a time, feeds the scoring pipeline,
    /// and writes result.json back into the same folder. Avoids the single-request
    /// size limit — no payload ever travels in one giant HTTP body.
    /// </summary>
    public class BlobAnalysisService : IBlobAnalysisService
    {
        private readonly IMsmeDataStore _store;
        private readonly IRiskScoringService _scoring;
        private readonly IBankAdminService _bankAdmin;
        private readonly ILogger<BlobAnalysisService> _logger;

        public BlobAnalysisService(IMsmeDataStore store, IRiskScoringService scoring,
            IBankAdminService bankAdmin, ILogger<BlobAnalysisService> logger)
        {
            _store = store;
            _scoring = scoring;
            _bankAdmin = bankAdmin;
            _logger = logger;
        }

        public async Task SaveManifestAsync(string uan, MsmeDataManifest.ExpectedFiles expected, CancellationToken ct = default)
        {
            // Preserve creation time if the manifest already exists (re-declares are allowed)
            var existing = await LoadManifestAsync(uan, ct);
            var manifest = new MsmeDataManifest
            {
                MsmeRef = uan,
                Expected = expected,
                Status = MsmeDataStatus.Collecting,
                CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow
            };
            await WriteManifestAsync(uan, manifest, ct);
        }

        public async Task<MsmeDataStatusReport> GetStatusAsync(string uan, CancellationToken ct = default)
        {
            var manifest = await LoadManifestAsync(uan, ct);
            var files = (await _store.ListFilesAsync(uan, ct))
                .Where(f => f != MsmeDataFiles.Manifest && f != MsmeDataFiles.Result)
                .ToList();

            var report = new MsmeDataStatusReport
            {
                Uan = uan,
                Manifest = manifest,
                FilesPresent = files,
                ResultAvailable = await _store.ExistsAsync(uan, MsmeDataFiles.Result, ct)
            };

            if (manifest != null)
            {
                report.FilesMissing = ComputeMissing(manifest.Expected, files);
                report.ReadyForAnalysis = report.FilesMissing.Count == 0;
            }
            else
            {
                // No manifest: anything present is usable, but "ready" can't be asserted
                report.ReadyForAnalysis = files.Count > 0;
            }
            return report;
        }

        public async Task<RiskAnalysisResult> AnalyzeAsync(string uan, bool force = false, CancellationToken ct = default)
        {
            var status = await GetStatusAsync(uan, ct);
            if (status.FilesPresent.Count == 0)
                throw new InvalidOperationException($"No data files found in folder '{uan}'.");
            if (!force && status.Manifest != null && status.FilesMissing.Count > 0)
                throw new InvalidOperationException(
                    $"Folder '{uan}' is not complete yet. Missing: {string.Join(", ", status.FilesMissing)}. "
                    + "Upload the remaining files or call with force=true to analyze partial data.");

            var manifest = status.Manifest ?? new MsmeDataManifest { MsmeRef = uan };
            manifest.Status = MsmeDataStatus.Processing;
            await WriteManifestAsync(uan, manifest, ct);

            try
            {
                // Download every needed file in parallel (bounded). 40+ sequential blob
                // round-trips from a shared host easily blow past the reverse-proxy
                // response timeout — the request would die mid-run ("Failed to fetch")
                // before result.json was written. Payloads are small JSON (~200 KB), so
                // holding them briefly in one dictionary is fine.
                var fixedFiles = new[] { MsmeDataFiles.Udyam, MsmeDataFiles.Itr, MsmeDataFiles.Aa,
                                         MsmeDataFiles.GstTaxpayer, MsmeDataFiles.Epfo };
                var toDownload = fixedFiles.Concat(status.FilesPresent)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                var downloaded = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                using (var throttler = new SemaphoreSlim(8))
                {
                    await Task.WhenAll(toDownload.Select(async file =>
                    {
                        await throttler.WaitAsync(ct);
                        try { downloaded[file] = await _store.DownloadAsync(uan, file, ct); }
                        finally { throttler.Release(); }
                    }));
                }

                string? Get(string name) => downloaded.TryGetValue(name, out var v) ? v : null;

                var request = new RiskAnalysisRequest
                {
                    UdyamJson = Get(MsmeDataFiles.Udyam),
                    ItrJson = Get(MsmeDataFiles.Itr),
                    AaJson = Get(MsmeDataFiles.Aa),
                    GstTaxpayerJson = Get(MsmeDataFiles.GstTaxpayer),
                    EpfoJson = Get(MsmeDataFiles.Epfo)
                };

                foreach (var file in status.FilesPresent.OrderBy(f => f))
                {
                    var content = Get(file) ?? string.Empty;
                    if (file.StartsWith(MsmeDataFiles.Gstr3bPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr3bJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr1SummaryPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr1SummaryJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr1CdnrPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr1CdnrJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr1HsnPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr1HsnJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr2aB2bPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr2aB2bJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr2bPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr2bJsons.Add(content);
                    else if (file.StartsWith(MsmeDataFiles.Gstr1B2bPrefix, StringComparison.OrdinalIgnoreCase))
                        request.Gstr1B2bJsons.Add(content);
                }

                // Diagnostics: confirm which sources actually reached the scorer.
                _logger.LogInformation(
                    "Analyze {Uan}: files=[{Files}] udyam={U} itr={I} aa={A} gstTaxpayer={G} gstr3b={G3}",
                    uan, string.Join(",", status.FilesPresent),
                    request.UdyamJson != null, request.ItrJson != null, request.AaJson != null,
                    request.GstTaxpayerJson != null, request.Gstr3bJsons.Count);

                if (!string.IsNullOrWhiteSpace(request.AaJson)
                    && !request.AaJson.Contains("fiObjects", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Analyze {Uan}: aa.json present ({Len} chars) but has no 'fiObjects' node — the AA "
                        + "extractor expects decrypted FI data (body[*].fiObjects[*]); this looks like the raw/"
                        + "encrypted Finvu FIFetch response, so AA will NOT contribute to the score.",
                        uan, request.AaJson.Length);
                }

                var result = _scoring.Analyze(request);

                await _store.UploadAsync(uan, MsmeDataFiles.Result,
                    JsonConvert.SerializeObject(result, Formatting.Indented), ct);

                // Mirror the headline figures into t_MsmeScoreSummary so the bank
                // portal can list, sort and chart every MSME from one SQL query
                // instead of reading each result.json. The blob stays the source of
                // truth — a DB hiccup must not fail an otherwise good analysis.
                try
                {
                    await _bankAdmin.SaveScoreSummaryAsync(uan, result, ct);
                }
                catch (Exception summaryEx)
                {
                    _logger.LogError(summaryEx,
                        "Analyze {Uan}: score computed and result.json written, but the "
                        + "t_MsmeScoreSummary upsert failed — the bank portal list may be stale.", uan);
                }

                manifest.Status = MsmeDataStatus.Completed;
                manifest.CompletedAt = DateTime.UtcNow;
                manifest.Error = null;
                await WriteManifestAsync(uan, manifest, ct);
                return result;
            }
            catch (Exception ex)
            {
                manifest.Status = MsmeDataStatus.Failed;
                manifest.Error = ex.Message;
                await WriteManifestAsync(uan, manifest, ct);
                throw;
            }
        }

        public async Task<RiskAnalysisResult?> GetResultAsync(string uan, CancellationToken ct = default)
        {
            var json = await _store.DownloadAsync(uan, MsmeDataFiles.Result, ct);
            return json == null ? null : JsonConvert.DeserializeObject<RiskAnalysisResult>(json);
        }

        private static List<string> ComputeMissing(MsmeDataManifest.ExpectedFiles expected, List<string> present)
        {
            var missing = new List<string>();
            void Require(bool isExpected, string file)
            {
                if (isExpected && !present.Contains(file, StringComparer.OrdinalIgnoreCase))
                    missing.Add(file);
            }

            Require(expected.Udyam, MsmeDataFiles.Udyam);
            Require(expected.Itr, MsmeDataFiles.Itr);
            Require(expected.Aa, MsmeDataFiles.Aa);
            Require(expected.GstTaxpayer, MsmeDataFiles.GstTaxpayer);
            Require(expected.Epfo, MsmeDataFiles.Epfo);

            var gstr3bCount = present.Count(f => f.StartsWith(MsmeDataFiles.Gstr3bPrefix, StringComparison.OrdinalIgnoreCase));
            if (gstr3bCount < expected.GstMonths)
                missing.Add($"{MsmeDataFiles.Gstr3bPrefix}* ({gstr3bCount} of {expected.GstMonths} months uploaded)");

            return missing;
        }

        private async Task<MsmeDataManifest?> LoadManifestAsync(string uan, CancellationToken ct)
        {
            var json = await _store.DownloadAsync(uan, MsmeDataFiles.Manifest, ct);
            return json == null ? null : JsonConvert.DeserializeObject<MsmeDataManifest>(json);
        }

        private Task WriteManifestAsync(string uan, MsmeDataManifest manifest, CancellationToken ct)
            => _store.UploadAsync(uan, MsmeDataFiles.Manifest,
                JsonConvert.SerializeObject(manifest, Formatting.Indented), ct);
    }
}
