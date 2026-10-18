using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Core.Models.Storage;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FinRiskLensAI.ML.Services
{
    /// <summary>
    /// The MSME's data folder (named by Udyam number): source payloads arrive file by
    /// file, a manifest says what to expect, and <see cref="GetFactsAsync"/> reads the
    /// folder and builds the facts. Uploading file-by-file avoids the single-request
    /// size limit — no payload ever travels in one giant HTTP body.
    /// </summary>
    public class BlobAnalysisService : IBlobAnalysisService
    {
        private readonly IMsmeDataStore _store;
        private readonly ISourceFactsService _facts;
        private readonly IRiskScoringService _scoring;
        private readonly ILogger<BlobAnalysisService> _logger;

        public BlobAnalysisService(IMsmeDataStore store, ISourceFactsService facts,
            IRiskScoringService scoring,
            ILogger<BlobAnalysisService> logger)
        {
            _store = store;
            _facts = facts;
            _scoring = scoring;
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
            var files = await ListDataFilesAsync(uan, ct);

            var report = new MsmeDataStatusReport { Uan = uan, Manifest = manifest, FilesPresent = files };

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

        public async Task<SourceFacts?> GetFactsAsync(string uan, CancellationToken ct = default)
        {
            var files = await ListDataFilesAsync(uan, ct);
            if (files.Count == 0) return null;

            // Download in parallel (bounded). 40+ sequential blob round-trips from a
            // shared host blow past the reverse-proxy timeout; payloads are small JSON.
            var downloaded = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            using (var throttler = new SemaphoreSlim(8))
            {
                await Task.WhenAll(files.Select(async file =>
                {
                    await throttler.WaitAsync(ct);
                    try { downloaded[file] = await _store.DownloadAsync(uan, file, ct); }
                    finally { throttler.Release(); }
                }));
            }

            var request = SourceFactsRequest.FromFiles(downloaded);

            if (!string.IsNullOrWhiteSpace(request.AaJson)
                && !request.AaJson.Contains("fiObjects", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Facts {Uan}: aa.json present ({Len} chars) but has no 'fiObjects' node — the AA "
                    + "extractor expects decrypted FI data (body[*].fiObjects[*]); this looks like the raw/"
                    + "encrypted Finvu FIFetch response, so bank facts will be empty.",
                    uan, request.AaJson.Length);
            }

            return _facts.Build(request);
        }

        public async Task<RiskAnalysisResult?> GetResultAsync(string uan, CancellationToken ct = default)
        {
            var facts = await GetFactsAsync(uan, ct);
            if (facts == null) return null;
            return _scoring.Analyze(facts);
        }

        private async Task<List<string>> ListDataFilesAsync(string uan, CancellationToken ct)
            => (await _store.ListFilesAsync(uan, ct))
                .Where(f => f != MsmeDataFiles.Manifest && f != MsmeDataFiles.LegacyResult)
                .ToList();

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
