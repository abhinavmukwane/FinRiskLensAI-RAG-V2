namespace FinRiskLensAI.Core.Models.Knowledge
{
    /// <summary>One retrieved chunk, with where it came from and how each arm ranked it.</summary>
    public sealed record KnowledgeHit(
        long ChunkId,
        int DocId,
        KnowledgeCorpus Corpus,
        string Title,
        string? Version,
        string? Breadcrumb,
        string Text)
    {
        /// <summary>Fused reciprocal-rank score; comparable only within one result list.</summary>
        public double Score { get; init; }
        /// <summary>1-based rank in the keyword arm, null if that arm did not return it.</summary>
        public int? SparseRank { get; init; }
        /// <summary>1-based rank in the vector arm, null if that arm did not return it.</summary>
        public int? DenseRank { get; init; }
        /// <summary>Reranker's 0–1 judgement of how well this answers the question; null when not reranked.</summary>
        public double? Relevance { get; init; }
    }

    /// <summary>Outcome of an ingest: a new version, or a no-op because the same bytes are already current.</summary>
    public sealed record KnowledgeIngestResult(int DocId, int Chunks, bool AlreadyCurrent, int Superseded);

    /// <summary>Retrieval knobs — Doc/11_RAG_ARCHITECTURE.md §16. Bound from the "Rag" section.</summary>
    public class RagOptions
    {
        public int SparseTopK { get; set; } = 20;
        public int DenseTopK { get; set; } = 20;
        public int RrfK { get; set; } = 60;
        public int FinalTopK { get; set; } = 5;
        /// <summary>Fused candidates the reranker judges before the final cut.</summary>
        public int RerankCandidates { get; set; } = 20;
        public bool Rerank { get; set; } = true;
    }
}
