namespace FinRiskLensAI.Fraud.Rag
{
    /// <summary>
    /// Reciprocal rank fusion (Doc/11_RAG_ARCHITECTURE.md §9). Each arm contributes
    /// 1 / (k + rank) for every id it returned; ids found by both arms rise. Ranks, not
    /// scores, are fused — a ts_rank and a cosine distance are not on the same scale, and
    /// RRF never has to pretend they are.
    /// </summary>
    public static class Rrf
    {
        /// <returns>Ids by fused score, best first; ties keep the order of first appearance.</returns>
        public static List<(long Id, double Score)> Fuse(IEnumerable<IReadOnlyList<long>> rankings, int k)
        {
            var scores = new Dictionary<long, double>();
            var firstSeen = new List<long>();
            foreach (var ranking in rankings)
            {
                for (int i = 0; i < ranking.Count; i++)
                {
                    var id = ranking[i];
                    if (!scores.ContainsKey(id)) { scores[id] = 0; firstSeen.Add(id); }
                    scores[id] += 1.0 / (k + i + 1);
                }
            }
            return firstSeen.Select((id, order) => (id, order))
                            .OrderByDescending(x => scores[x.id]).ThenBy(x => x.order)
                            .Select(x => (x.id, scores[x.id]))
                            .ToList();
        }
    }
}
