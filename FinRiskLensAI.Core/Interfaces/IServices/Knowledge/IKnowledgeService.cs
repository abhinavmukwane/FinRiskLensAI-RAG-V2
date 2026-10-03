using FinRiskLensAI.Core.Models.Knowledge;

namespace FinRiskLensAI.Core.Interfaces.IServices.Knowledge
{
    /// <summary>Ingest into and retrieve from the RAG corpora (Doc/11_RAG_ARCHITECTURE.md §5, §9).</summary>
    public interface IKnowledgeService
    {
        /// <summary>
        /// Chunks parsed markdown and stores it as the current version of <paramref name="title"/>.
        /// Re-ingesting identical content is a no-op. Embeddings are added separately (R3).
        /// </summary>
        Task<KnowledgeIngestResult> IngestMarkdownAsync(KnowledgeCorpus corpus, string title, string? version,
                                                        string markdown, string actor, CancellationToken ct = default);

        /// <summary>Retrieval never runs unscoped — the caller names the corpora (§3).</summary>
        Task<IReadOnlyList<KnowledgeHit>> SearchAsync(string query, IReadOnlyCollection<KnowledgeCorpus> corpora,
                                                      int? topK = null, CancellationToken ct = default);
    }
}
