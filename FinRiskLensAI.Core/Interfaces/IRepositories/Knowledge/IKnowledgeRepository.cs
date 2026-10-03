using FinRiskLensAI.Core.Models.Knowledge;

namespace FinRiskLensAI.Core.Interfaces.IRepositories.Knowledge
{
    public interface IKnowledgeRepository
    {
        /// <summary>The current document in this corpus with exactly these bytes, if any.</summary>
        Task<KnowledgeDoc?> FindCurrentByShaAsync(KnowledgeCorpus corpus, byte[] sha256, CancellationToken ct = default);

        /// <summary>
        /// Saves <paramref name="doc"/> with its chunks as the current version, in one
        /// transaction with retiring the previous current version of the same title in the
        /// same corpus. Old rows are marked not-current, never deleted, so past citations
        /// still resolve. Returns how many documents were superseded.
        /// </summary>
        Task<int> AddVersionAsync(KnowledgeDoc doc, CancellationToken ct = default);

        /// <summary>
        /// Keyword arm: full-text match over breadcrumb + chunk text, any query term
        /// counting, ranked by the summed rarity (IDF) of the terms matched. Current,
        /// in-force chunks of the named corpora only.
        /// </summary>
        Task<IReadOnlyList<long>> SparseSearchAsync(string query, IReadOnlyCollection<KnowledgeCorpus> corpora, int k,
                                                   CancellationToken ct = default);

        Task<IReadOnlyList<KnowledgeHit>> GetHitsAsync(IReadOnlyCollection<long> chunkIds, CancellationToken ct = default);
    }
}
