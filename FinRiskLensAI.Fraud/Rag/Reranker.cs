using System.Text;
using System.Text.Json.Nodes;
using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Fraud.Ai;

namespace FinRiskLensAI.Fraud.Rag
{
    /// <summary>
    /// Doc/11_RAG_ARCHITECTURE.md §10: Gemini Flash scores each fused candidate against the
    /// question. Fusion decides who is in the room; this decides the order. It is what stops a
    /// keyword arm's coincidental matches ("look", "sum") outranking the one chunk that answers,
    /// and its score is what an answerer refuses on when nothing is relevant enough.
    /// </summary>
    public class Reranker
    {
        private const string Instruction = """
            You judge how well each passage from a lender's knowledge base answers a question.
            Score every passage from 0 to 1: 1 = directly answers it, 0.5 = related but does not
            answer it, 0 = unrelated. Judge only what the passage says, not what it might imply.
            Return one score per passage id given.
            """;

        private const int PassageChars = 900;

        private readonly GeminiClient _gemini;

        public Reranker(GeminiClient gemini) => _gemini = gemini;

        public async Task<IReadOnlyList<KnowledgeHit>> RerankAsync(string question, IReadOnlyList<KnowledgeHit> candidates,
                                                                   CancellationToken ct = default)
        {
            if (candidates.Count <= 1) return candidates;

            var prompt = new StringBuilder($"Question: {question}\n\nPassages:\n");
            foreach (var h in candidates)
            {
                var text = h.Text.Length <= PassageChars ? h.Text : h.Text[..PassageChars] + "…";
                prompt.Append($"\n[id {h.ChunkId}] {h.Breadcrumb}\n{text}\n");
            }

            var schema = new JsonObject
            {
                ["type"] = "OBJECT",
                ["required"] = new JsonArray("scores"),
                ["properties"] = new JsonObject
                {
                    ["scores"] = new JsonObject
                    {
                        ["type"] = "ARRAY",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["required"] = new JsonArray("id", "score"),
                            ["properties"] = new JsonObject
                            {
                                ["id"] = new JsonObject { ["type"] = "INTEGER" },
                                ["score"] = new JsonObject { ["type"] = "NUMBER" }
                            }
                        }
                    }
                }
            };

            var o = _gemini.Options;
            var result = await _gemini.GenerateJsonAsync<Scores>(o.FlashModel, o.FlashLocation, Instruction,
                new JsonNode[] { GeminiClient.Text(prompt.ToString()) }, schema, maxOutputTokens: 4_000, "LOW", ct);

            // Ids the model invented are ignored; candidates it skipped keep their place, scored 0
            var score = (result.Items ?? new()).GroupBy(s => s.Id).ToDictionary(g => g.Key, g => Math.Clamp(g.First().Score, 0, 1));
            return candidates.Select((h, order) => (h, order, s: score.GetValueOrDefault(h.ChunkId)))
                             .OrderByDescending(x => x.s).ThenBy(x => x.order)
                             .Select(x => x.h with { Relevance = x.s })
                             .ToList();
        }

        private sealed class Scores
        {
            [System.Text.Json.Serialization.JsonPropertyName("scores")]
            public List<Item>? Items { get; set; }
        }

        private sealed class Item
        {
            public long Id { get; set; }
            public double Score { get; set; }
        }
    }
}
