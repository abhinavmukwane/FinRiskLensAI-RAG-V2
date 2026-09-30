using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Knowledge
{
    /// <summary>
    /// The five corpora the RAG layer retrieves from (Doc/11_RAG_ARCHITECTURE.md §3).
    /// Stored as names, not ints — adding a corpus must never renumber history.
    /// </summary>
    public enum KnowledgeCorpus
    {
        /// <summary>C1 — the lender's credit policy and verification SOPs.</summary>
        Policy,
        /// <summary>C2 — RBI master directions, DPDP obligations.</summary>
        Regulation,
        /// <summary>C3 — fraud modus-operandi catalogue.</summary>
        Typology,
        /// <summary>C4 — de-identified confirmed findings and ring narratives; grows with use.</summary>
        CaseMemory,
        /// <summary>C5 — layout descriptions of verified-genuine documents per issuer.</summary>
        Exemplar
    }

    /// <summary>
    /// One source document in a corpus. Never hard-deleted: a revision sets
    /// <see cref="IsCurrent"/> false on the old version, so a citation recorded
    /// against a past case still resolves to the text the underwriter actually read.
    /// </summary>
    public class KnowledgeDoc : AuditableEntity
    {
        public int KnowledgeDocID { get; set; }
        public KnowledgeCorpus Corpus { get; set; }
        public string Title { get; set; } = string.Empty;
        /// <summary>gs://… for uploaded sources; null for generated ones (C4, C5).</summary>
        public string? SourceUri { get; set; }
        /// <summary>SHA-256 of the source — re-ingesting an identical file is a no-op.</summary>
        public byte[] Sha256 { get; set; } = Array.Empty<byte>();
        /// <summary>Human label for citations, e.g. "Credit Policy v3".</summary>
        public string? Version { get; set; }
        public DateOnly? EffectiveFrom { get; set; }
        public DateOnly? EffectiveTo { get; set; }
        /// <summary>Tenant scope; null means the document applies to every lender.</summary>
        public int? LenderID { get; set; }
        public bool IsCurrent { get; set; } = true;

        public List<KnowledgeChunk> Chunks { get; set; } = new();
    }

    /// <summary>
    /// The retrievable unit. Chunking is per corpus (§6): heading-aware for policy
    /// and regulation, one atomic chunk per entry for typology, case memory and
    /// exemplars. The keyword index over <see cref="Breadcrumb"/> + <see cref="ChunkText"/>
    /// is a generated column that exists only in the database.
    /// </summary>
    public class KnowledgeChunk : AuditableEntity
    {
        public long KnowledgeChunkID { get; set; }
        public int KnowledgeDocID { get; set; }
        public KnowledgeDoc? KnowledgeDoc { get; set; }

        public int Ordinal { get; set; }
        /// <summary>Where the chunk sits, e.g. "Credit Policy v3 › §4 Exposure › §4.2". Shown beside citations.</summary>
        public string? Breadcrumb { get; set; }
        public string ChunkText { get; set; } = string.Empty;
        public int TokenCount { get; set; }

        /// <summary>C4 provenance — the confirmed finding or ring this memory came from.</summary>
        public int? SourceFindingID { get; set; }
        public int? SourceRingID { get; set; }
        public bool IsCurrent { get; set; } = true;

        public List<ChunkEmbedding> Embeddings { get; set; } = new();
    }

    /// <summary>
    /// A chunk's vector under one embedding model. Unique per (chunk, model), so a
    /// re-embed under a new model writes alongside the old vectors and retrieval cuts
    /// over by filtering on <see cref="ModelId"/> — no window where search is empty.
    /// </summary>
    public class ChunkEmbedding : AuditableEntity
    {
        /// <summary>
        /// Width of the stored vector. Request this output dimensionality from the
        /// embedding model; pgvector's HNSW index supports up to 2,000 dimensions on
        /// the vector type, so larger native outputs must be truncated to fit.
        /// </summary>
        public const int VectorDimensions = 768;

        public long ChunkEmbeddingID { get; set; }
        public long KnowledgeChunkID { get; set; }
        public KnowledgeChunk? KnowledgeChunk { get; set; }

        public string ModelId { get; set; } = string.Empty;
        public int Dimensions { get; set; }
        /// <summary>Normalised, so cosine distance and inner product agree.</summary>
        public float[] Embedding { get; set; } = Array.Empty<float>();
    }

    /// <summary>
    /// Every retrieval, whoever asked — an underwriter or the Investigator agent — with
    /// what was retrieved and how grounding verification judged the answer. This is
    /// the audit trail that makes shown guidance reproducible.
    /// </summary>
    public class RagQueryLog : AuditableEntity
    {
        public long RagQueryLogID { get; set; }
        public int? ApplicationID { get; set; }
        /// <summary>User id, or "INVESTIGATOR_AGENT".</summary>
        public string Actor { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        /// <summary>Comma-separated corpus names the query was scoped to.</summary>
        public string Corpora { get; set; } = string.Empty;
        /// <summary>JSON: chunk ids with their fused scores. Stored as jsonb so evals can query it.</summary>
        public string RetrievedIds { get; set; } = "[]";
        public string? Answer { get; set; }
        /// <summary>GROUNDED | PARTIAL | REFUSED | BLOCKED.</summary>
        public string Grounded { get; set; } = string.Empty;
        public int StrippedClaims { get; set; }
        public int LatencyMs { get; set; }
    }
}
