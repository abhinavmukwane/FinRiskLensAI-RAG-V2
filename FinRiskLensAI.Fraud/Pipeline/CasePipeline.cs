using System.Threading.Channels;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinRiskLensAI.Fraud.Pipeline
{
    /// <summary>Applications waiting for the pipeline. In-process: Cloud Run runs one instance with --min-instances=1.</summary>
    public class CaseQueue
    {
        private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

        public void Enqueue(int loanApplicationId) => _channel.Writer.TryWrite(loanApplicationId);

        public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// Upload → ② extract (Gemini, page by page) → ④ deterministic checks → Review.
    /// One application at a time; its documents are read in parallel. On start it re-queues
    /// anything a restart interrupted, so a document never sits at Pending for good.
    /// </summary>
    public class CasePipelineWorker : BackgroundService
    {
        private const string Actor = "PIPELINE";

        private readonly CaseQueue _queue;
        private readonly IServiceScopeFactory _scopes;
        private readonly GeminiOptions _gemini;
        private readonly ILogger<CasePipelineWorker> _log;

        public CasePipelineWorker(CaseQueue queue, IServiceScopeFactory scopes, GeminiOptions gemini, ILogger<CasePipelineWorker> log)
        {
            _queue = queue;
            _scopes = scopes;
            _gemini = gemini;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_gemini.IsConfigured)
            {
                _log.LogWarning("Gcp:ProjectId is not set — documents will stay at Pending until it is");
                return;
            }

            try
            {
                using var scope = _scopes.CreateScope();
                var waiting = await scope.ServiceProvider.GetRequiredService<ICaseRepository>().GetAwaitingExtractionAsync(stoppingToken);
                foreach (var id in waiting) _queue.Enqueue(id);
                if (waiting.Count > 0) _log.LogInformation("Re-queued {Count} application(s) with documents awaiting extraction", waiting.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Could not scan for documents awaiting extraction");
            }

            await foreach (var id in _queue.ReadAllAsync(stoppingToken))
            {
                try { await ProcessAsync(id, stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Pipeline failed for case {CaseId}", id);
                }
            }
        }

        private async Task ProcessAsync(int id, CancellationToken ct)
        {
            using var scope = _scopes.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<ICaseRepository>();
            var cases = scope.ServiceProvider.GetRequiredService<ICaseService>();
            var store = scope.ServiceProvider.GetRequiredService<ICaseDocumentStore>();
            var extractor = scope.ServiceProvider.GetRequiredService<DocumentExtractor>();

            var application = await repo.GetAsync(id, ct);
            var pending = application?.Documents.Where(d => d.ExtractStatus == ExtractStatus.Pending).ToList();
            if (pending == null || pending.Count == 0) return;

            await repo.SetStatusAsync(id, CaseStatus.Processing, ct);
            var started = DateTime.UtcNow;

            // Read in parallel; save one at a time — the scope's DbContext is not thread-safe
            var reads = await Task.WhenAll(pending.Select(async doc =>
            {
                try
                {
                    await using var stream = await store.OpenReadAsync(doc.StoragePath, ct)
                        ?? throw new InvalidOperationException("The stored file is missing.");
                    using var buffer = new MemoryStream();
                    await stream.CopyToAsync(buffer, ct);
                    return (doc, Extracted: await extractor.ExtractAsync(buffer.ToArray(), doc.ContentType, ct), Error: (string?)null);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    return (doc, Extracted: (ExtractedDocument?)null, Error: ex.Message);
                }
            }));

            foreach (var (doc, extracted, error) in reads)
            {
                if (extracted != null) await cases.ApplyExtractionAsync(id, doc.CaseDocumentID, extracted, Actor, ct);
                else
                {
                    _log.LogWarning("Extraction failed for document {DocId} of case {CaseId}: {Error}", doc.CaseDocumentID, id, error);
                    await repo.MarkExtractionFailedAsync(doc.CaseDocumentID, error!, ct);
                }
            }

            await cases.RunChecksAsync(id, ct);
            await repo.SetStatusAsync(id, CaseStatus.Review, ct);
            _log.LogInformation("Case {CaseId}: {Count} document(s) through the pipeline in {Seconds:0}s",
                id, pending.Count, (DateTime.UtcNow - started).TotalSeconds);
        }
    }
}
