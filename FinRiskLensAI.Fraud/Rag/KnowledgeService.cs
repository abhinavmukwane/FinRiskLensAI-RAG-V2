using System.Security.Cryptography;
using System.Text;
using FinRiskLensAI.Core.Interfaces.IRepositories.Knowledge;
using FinRiskLensAI.Core.Interfaces.IServices.Knowledge;
using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Fraud.Ai;

namespace FinRiskLensAI.Fraud.Rag
{
    public class KnowledgeService : IKnowledgeService
    {
        private const int EmbedBatch = 32;

        private readonly IKnowledgeRepository _knowledge;
        private readonly RagOptions _options;
        private readonly GeminiClient? _gemini;

        /// <param name="gemini">Without it (tests, no GCP project) retrieval is keyword-only.</param>
        public KnowledgeService(IKnowledgeRepository knowledge, RagOptions options, GeminiClient? gemini = null)
        {
            _knowledge = knowledge;
            _options = options;
            _gemini = gemini?.Options.IsConfigured == true ? gemini : null;
        }

        public async Task<KnowledgeIngestResult> IngestMarkdownAsync(KnowledgeCorpus corpus, string title, string? version,
                                                                     string markdown, string actor, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("A title is required — it roots every citation.", nameof(title));

            var sha = SHA256.HashData(Encoding.UTF8.GetBytes(markdown.Replace("\r\n", "\n")));
            var existing = await _knowledge.FindCurrentByShaAsync(corpus, sha, ct);
            if (existing != null) return new(existing.KnowledgeDocID, 0, AlreadyCurrent: true, Superseded: 0);

            // The breadcrumb root is what a citation reads as: "Credit Policy v3 › §4 › §4.2"
            var root = string.IsNullOrWhiteSpace(version) ? title.Trim() : $"{title.Trim()} {version.Trim()}";
            var drafts = MarkdownChunker.Chunk(root, markdown, MarkdownChunker.For(corpus));

            var doc = new KnowledgeDoc
            {
                Corpus = corpus,
                Title = title.Trim(),
                Version = string.IsNullOrWhiteSpace(version) ? null : version.Trim(),
                Sha256 = sha,
                CreatedBy = actor,
                UpdatedBy = actor,
                Chunks = drafts.Select(d => new KnowledgeChunk
                {
                    Ordinal = d.Ordinal,
                    Breadcrumb = d.Breadcrumb,
                    ChunkText = d.Text,
                    TokenCount = d.TokenCount,
                    CreatedBy = actor,
                    UpdatedBy = actor
                }).ToList()
            };

            var superseded = await _knowledge.AddVersionAsync(doc, ct);
            await EmbedMissingAsync(ct);
            return new(doc.KnowledgeDocID, drafts.Count, AlreadyCurrent: false, superseded);
        }

        public async Task<int> EmbedMissingAsync(CancellationToken ct = default)
        {
            if (_gemini == null) return 0;
            var model = _gemini.Options.EmbeddingModel;
            int total = 0;
            while (true)
            {
                var batch = await _knowledge.GetUnembeddedAsync(model, EmbedBatch, ct);
                if (batch.Count == 0) return total;

                var vectors = await _gemini.EmbedAsync(batch.Select(b => b.Text).ToList(), "RETRIEVAL_DOCUMENT", ct);
                await _knowledge.AddEmbeddingsAsync(batch.Select((b, i) => new ChunkEmbedding
                {
                    KnowledgeChunkID = b.ChunkId,
                    ModelId = model,
                    Dimensions = vectors[i].Length,
                    Embedding = vectors[i],
                    CreatedBy = "EMBEDDER",
                    UpdatedBy = "EMBEDDER"
                }).ToList(), ct);
                total += batch.Count;
            }
        }

        public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, IReadOnlyCollection<KnowledgeCorpus> corpora,
                                                                   int? topK = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || corpora.Count == 0) return Array.Empty<KnowledgeHit>();

            var sparse = await _knowledge.SparseSearchAsync(query, corpora, _options.SparseTopK, ct);
            IReadOnlyList<long> dense = Array.Empty<long>();
            if (_gemini != null)
            {
                // RETRIEVAL_QUERY for the question, RETRIEVAL_DOCUMENT for the chunks — mixing them up degrades recall (§7)
                var vector = (await _gemini.EmbedAsync(new[] { query }, "RETRIEVAL_QUERY", ct))[0];
                dense = await _knowledge.DenseSearchAsync(vector, _gemini.Options.EmbeddingModel, corpora, _options.DenseTopK, ct);
            }
            var rerank = _gemini != null && _options.Rerank;
            var fused = Rrf.Fuse(new[] { sparse, dense }, _options.RrfK)
                           .Take(rerank ? Math.Max(_options.RerankCandidates, topK ?? _options.FinalTopK) : topK ?? _options.FinalTopK)
                           .ToList();
            if (fused.Count == 0) return Array.Empty<KnowledgeHit>();

            var hits = (await _knowledge.GetHitsAsync(fused.Select(f => f.Id).ToList(), ct)).ToDictionary(h => h.ChunkId);
            var sparseRank = sparse.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i + 1);
            var denseRank = dense.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i + 1);

            var ranked = fused.Where(f => hits.ContainsKey(f.Id))
                              .Select(f => hits[f.Id] with
                              {
                                  Score = f.Score,
                                  SparseRank = sparseRank.TryGetValue(f.Id, out var s) ? s : null,
                                  DenseRank = denseRank.TryGetValue(f.Id, out var d) ? d : null
                              })
                              .ToList();
            if (rerank) ranked = (await new Reranker(_gemini!).RerankAsync(query, ranked, ct)).ToList();
            return ranked.Take(topK ?? _options.FinalTopK).ToList();
        }
    }
}
