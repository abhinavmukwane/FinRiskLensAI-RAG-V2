using FinRiskLensAI.Core.Models.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NpgsqlTypes;
using Pgvector;

namespace FinRiskLensAI.Data.Configurations
{
    // The RAG knowledge base — Doc/11_RAG_ARCHITECTURE.md §14. Four related tables,
    // one file. Retrieval itself is hand-written SQL (dense: pgvector <=>, sparse:
    // tsvector @@), so the index definitions here matter more than the navigations.

    public class KnowledgeDocConfiguration : IEntityTypeConfiguration<KnowledgeDoc>
    {
        public void Configure(EntityTypeBuilder<KnowledgeDoc> builder)
        {
            builder.ToTable("knowledge_docs");
            builder.HasKey(x => x.KnowledgeDocID);

            builder.Property(x => x.Corpus).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(x => x.Title).IsRequired();
            builder.Property(x => x.Sha256).IsRequired();
            builder.Property(x => x.Version).HasMaxLength(40);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Retrieval always pre-filters on corpus + current before any distance scan.
            builder.HasIndex(x => new { x.Corpus, x.IsCurrent });
            builder.HasIndex(x => x.Sha256);   // idempotent re-ingest lookup
        }
    }

    public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
    {
        public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
        {
            builder.ToTable("knowledge_chunks");
            builder.HasKey(x => x.KnowledgeChunkID);

            builder.Property(x => x.ChunkText).IsRequired();
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasOne(x => x.KnowledgeDoc).WithMany(d => d.Chunks)
                   .HasForeignKey(x => x.KnowledgeDocID).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.KnowledgeDocID, x.Ordinal });

            // Sparse arm of hybrid retrieval. A generated column keeps the full-text
            // index correct on every insert and update — no post-ingest step to forget.
            // Shadow property: Core stays free of Npgsql types, and only the hand-written
            // retrieval SQL ever reads it.
            builder.Property<NpgsqlTsVector>("SearchVector")
                   .HasComputedColumnSql(
                       "to_tsvector('english', coalesce(breadcrumb, '') || ' ' || chunk_text)", stored: true);
            builder.HasIndex("SearchVector").HasMethod("GIN");
        }
    }

    public class ChunkEmbeddingConfiguration : IEntityTypeConfiguration<ChunkEmbedding>
    {
        public void Configure(EntityTypeBuilder<ChunkEmbedding> builder)
        {
            builder.ToTable("chunk_embeddings");
            builder.HasKey(x => x.ChunkEmbeddingID);

            builder.Property(x => x.ModelId).HasMaxLength(100).IsRequired();
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Core holds a plain float[]; pgvector's Vector type lives only in this project.
            builder.Property(x => x.Embedding)
                   .HasColumnType($"vector({ChunkEmbedding.VectorDimensions})")
                   .HasConversion(
                       new ValueConverter<float[], Vector>(v => new Vector(v), v => v.ToArray()),
                       new ValueComparer<float[]>(
                           (a, b) => a!.SequenceEqual(b!),
                           a => a.Aggregate(0, (h, f) => HashCode.Combine(h, f)),
                           a => a.ToArray()))
                   .IsRequired();

            builder.HasOne(x => x.KnowledgeChunk).WithMany(c => c.Embeddings)
                   .HasForeignKey(x => x.KnowledgeChunkID).OnDelete(DeleteBehavior.Cascade);

            // One vector per chunk per model — old and new coexist during a re-embed.
            builder.HasIndex(x => new { x.KnowledgeChunkID, x.ModelId }).IsUnique();

            // Dense arm. HNSW over IVFFlat: no training step, tolerates a corpus that grows
            // continuously (case memory does), recall tuned at query time via hnsw.ef_search.
            builder.HasIndex(x => x.Embedding)
                   .HasMethod("hnsw")
                   .HasOperators("vector_cosine_ops")
                   .HasStorageParameter("m", 16)
                   .HasStorageParameter("ef_construction", 64);
        }
    }

    public class RagQueryLogConfiguration : IEntityTypeConfiguration<RagQueryLog>
    {
        public void Configure(EntityTypeBuilder<RagQueryLog> builder)
        {
            builder.ToTable("rag_query_log");
            builder.HasKey(x => x.RagQueryLogID);

            builder.Property(x => x.Actor).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Question).IsRequired();
            builder.Property(x => x.Corpora).HasMaxLength(100).IsRequired();
            builder.Property(x => x.RetrievedIds).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Grounded).HasMaxLength(20).IsRequired();
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasIndex(x => x.LoanApplicationID);
        }
    }
}
