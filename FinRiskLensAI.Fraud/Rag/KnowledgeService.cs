using System.Security.Cryptography;
using System.Text;
using FinRiskLensAI.Core.Interfaces.IRepositories.Knowledge;
using FinRiskLensAI.Core.Interfaces.IServices.Knowledge;
using FinRiskLensAI.Core.Models.Knowledge;

namespace FinRiskLensAI.Fraud.Rag
{
    public class KnowledgeService : IKnowledgeService
    {
        private readonly IKnowledgeRepository _knowledge;
        private readonly RagOptions _options;

        public KnowledgeService(IKnowledgeRepository knowledge, RagOptions options)
        {
            _knowledge = knowledge;
            _options = options;
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
            return new(doc.KnowledgeDocID, drafts.Count, AlreadyCurrent: false, superseded);
        }

        public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, IReadOnlyCollection<KnowledgeCorpus> corpora,
                                                                   int? topK = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(query) || corpora.Count == 0) return Array.Empty<KnowledgeHit>();

            var sparse = await _knowledge.SparseSearchAsync(query, corpora, _options.SparseTopK, ct);
            // ponytail: one arm until R3/R4 bring query embeddings — the dense ranking joins this list then
            var fused = Rrf.Fuse(new[] { sparse }, _options.RrfK).Take(topK ?? _options.FinalTopK).ToList();
            if (fused.Count == 0) return Array.Empty<KnowledgeHit>();

            var hits = (await _knowledge.GetHitsAsync(fused.Select(f => f.Id).ToList(), ct)).ToDictionary(h => h.ChunkId);
            var sparseRank = sparse.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i + 1);

            return fused.Where(f => hits.ContainsKey(f.Id))
                        .Select(f => hits[f.Id] with { Score = f.Score, SparseRank = sparseRank.TryGetValue(f.Id, out var r) ? r : null })
                        .ToList();
        }
    }
}
