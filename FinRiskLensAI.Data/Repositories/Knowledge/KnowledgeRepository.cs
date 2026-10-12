using System.Data;
using System.Globalization;
using FinRiskLensAI.Core.Interfaces.IRepositories.Knowledge;
using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Data.DbContextEDMX;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinRiskLensAI.Data.Repositories.Knowledge
{
    public class KnowledgeRepository : IKnowledgeRepository
    {
        private readonly ApplicationDbContext _context;

        public KnowledgeRepository(ApplicationDbContext context) => _context = context;

        public Task<KnowledgeDoc?> FindCurrentByShaAsync(KnowledgeCorpus corpus, byte[] sha256, CancellationToken ct = default)
            => _context.KnowledgeDocs.AsNoTracking()
                   .FirstOrDefaultAsync(d => d.Corpus == corpus && d.Sha256 == sha256 && d.IsCurrent, ct);

        public async Task<int> AddVersionAsync(KnowledgeDoc doc, CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var previous = _context.KnowledgeDocs.Where(d => d.Corpus == doc.Corpus && d.Title == doc.Title && d.IsCurrent);
            await _context.KnowledgeChunks
                .Where(c => previous.Select(d => d.KnowledgeDocID).Contains(c.KnowledgeDocID))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.IsCurrent, false)
                                          .SetProperty(c => c.UpdatedAt, DateTime.UtcNow), ct);
            var superseded = await previous
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsCurrent, false)
                                          .SetProperty(d => d.UpdatedAt, DateTime.UtcNow), ct);

            _context.KnowledgeDocs.Add(doc);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return superseded;
        }

        public async Task<IReadOnlyList<long>> SparseSearchAsync(string query, IReadOnlyCollection<KnowledgeCorpus> corpora,
                                                                int k, CancellationToken ct = default)
        {
            // Any query term may match (plainto_tsquery's ANDs become ORs — a question in
            // plain words rarely contains every term of the clause it is after), and the GIN
            // index finds the candidates. Ranking is by summed BM25-style IDF of the terms
            // each chunk contains: ts_rank_cd has no notion of term rarity, so "applicant"
            // and "account" would outvote the one word that matters in "applicant pays
            // betting apps". Document frequency comes from ts_stat over current chunks.
            // ponytail: ts_stat scans the corpus per query — fine at thousands of chunks;
            // keep a lexeme→df table refreshed on ingest if the corpus grows past that.
            const string sql = """
                WITH terms AS (
                    SELECT DISTINCT unnest(tsvector_to_array(to_tsvector('english', @query))) AS lexeme
                ),
                total AS (
                    SELECT count(*)::float8 AS n FROM knowledge_chunks WHERE is_current
                ),
                weights AS (
                    SELECT t.lexeme, ln(1 + (total.n - s.ndoc + 0.5) / (s.ndoc + 0.5)) AS idf
                    FROM terms t
                    JOIN ts_stat('SELECT search_vector FROM knowledge_chunks WHERE is_current') s ON s.word = t.lexeme
                    CROSS JOIN total
                ),
                candidates AS (
                    SELECT c.knowledge_chunk_id, c.search_vector
                    FROM knowledge_chunks c
                    JOIN knowledge_docs d ON d.knowledge_doc_id = c.knowledge_doc_id
                    WHERE c.search_vector @@ replace(plainto_tsquery('english', @query)::text, ' & ', ' | ')::tsquery
                      AND c.is_current AND d.is_current
                      AND d.corpus = ANY(@corpora)
                      AND (d.effective_from IS NULL OR d.effective_from <= current_date)
                      AND (d.effective_to   IS NULL OR d.effective_to   >= current_date)
                )
                SELECT c.knowledge_chunk_id
                FROM candidates c
                JOIN weights w ON w.lexeme = ANY(tsvector_to_array(c.search_vector))
                GROUP BY c.knowledge_chunk_id
                ORDER BY sum(w.idf) DESC, c.knowledge_chunk_id
                LIMIT @k
                """;

            return await QueryIdsAsync(sql, ct,
                ("query", query), ("corpora", corpora.Select(c => c.ToString()).ToArray()), ("k", k));
        }

        /// <summary>Raw SQL on the context's connection, so the snake_case convention never remaps result columns.</summary>
        private async Task<IReadOnlyList<long>> QueryIdsAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
        {
            var conn = (NpgsqlConnection)_context.Database.GetDbConnection();
            var opened = conn.State != ConnectionState.Open;
            if (opened) await conn.OpenAsync(ct);
            try
            {
                await using var cmd = new NpgsqlCommand(sql, conn);
                foreach (var (name, value) in parameters) cmd.Parameters.AddWithValue(name, value);

                var ids = new List<long>();
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) ids.Add(reader.GetInt64(0));
                return ids;
            }
            finally
            {
                if (opened) await conn.CloseAsync();
            }
        }

        public async Task<IReadOnlyList<long>> DenseSearchAsync(float[] query, string modelId,
                                                               IReadOnlyCollection<KnowledgeCorpus> corpora, int k,
                                                               CancellationToken ct = default)
        {
            const string sql = """
                SELECT c.knowledge_chunk_id
                FROM chunk_embeddings e
                JOIN knowledge_chunks c ON c.knowledge_chunk_id = e.knowledge_chunk_id
                JOIN knowledge_docs d   ON d.knowledge_doc_id = c.knowledge_doc_id
                WHERE e.model_id = @model
                  AND c.is_current AND d.is_current
                  AND d.corpus = ANY(@corpora)
                  AND (d.effective_from IS NULL OR d.effective_from <= current_date)
                  AND (d.effective_to   IS NULL OR d.effective_to   >= current_date)
                ORDER BY e.embedding <=> @query::vector
                LIMIT @k
                """;

            // The vector goes over as pgvector's text form and is cast in SQL — no type plugin
            // needed on a raw command.
            var literal = "[" + string.Join(",", query.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
            return await QueryIdsAsync(sql, ct,
                ("model", modelId), ("corpora", corpora.Select(c => c.ToString()).ToArray()), ("query", literal), ("k", k));
        }

        public async Task<IReadOnlyList<(long ChunkId, string Text)>> GetUnembeddedAsync(string modelId, int limit,
                                                                                       CancellationToken ct = default)
        {
            var rows = await _context.KnowledgeChunks.AsNoTracking()
                .Where(c => c.IsCurrent && !c.Embeddings.Any(e => e.ModelId == modelId))
                .OrderBy(c => c.KnowledgeChunkID)
                .Take(limit)
                .Select(c => new { c.KnowledgeChunkID, c.Breadcrumb, c.ChunkText })
                .ToListAsync(ct);
            // Same text the chunker embeds: breadcrumb first (§6, contextual prefixing)
            return rows.Select(r => (r.KnowledgeChunkID, (r.Breadcrumb ?? "") + "\n\n" + r.ChunkText)).ToList();
        }

        public async Task AddEmbeddingsAsync(IReadOnlyList<ChunkEmbedding> embeddings, CancellationToken ct = default)
        {
            _context.ChunkEmbeddings.AddRange(embeddings);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<KnowledgeHit>> GetHitsAsync(IReadOnlyCollection<long> chunkIds, CancellationToken ct = default)
            => await _context.KnowledgeChunks.AsNoTracking()
                   .Where(c => chunkIds.Contains(c.KnowledgeChunkID))
                   .Select(c => new KnowledgeHit(c.KnowledgeChunkID, c.KnowledgeDocID, c.KnowledgeDoc!.Corpus,
                                                 c.KnowledgeDoc.Title, c.KnowledgeDoc.Version, c.Breadcrumb, c.ChunkText))
                   .ToListAsync(ct);
    }
}
