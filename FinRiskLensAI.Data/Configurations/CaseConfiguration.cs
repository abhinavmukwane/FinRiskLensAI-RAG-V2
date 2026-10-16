using FinRiskLensAI.Core.Models.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    // Case tables — Doc/10_FRAUD_LENS_BUILD.md §4. The deterministic checks (Phase 3)
    // are hand-written SQL over statement_rows, so the indexes match their access paths.

    public class LoanApplicationConfiguration : IEntityTypeConfiguration<LoanApplication>
    {
        public void Configure(EntityTypeBuilder<LoanApplication> builder)
        {
            builder.ToTable("applications");
            builder.HasKey(x => x.LoanApplicationID);

            builder.Property(x => x.ApplicantRef).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ProductType).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Uan).HasMaxLength(30);
            builder.Property(x => x.DeclaredIncome).HasPrecision(14, 2);
            builder.Property(x => x.DeclaredEmployer).HasMaxLength(200);
            builder.Property(x => x.DeclaredEmis).HasPrecision(14, 2);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
            builder.Property(x => x.InvestigatedBy).HasMaxLength(60);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasIndex(x => x.SubmittedAt);
            builder.HasIndex(x => x.Uan);
        }
    }

    public class CaseDocumentConfiguration : IEntityTypeConfiguration<CaseDocument>
    {
        public void Configure(EntityTypeBuilder<CaseDocument> builder)
        {
            builder.ToTable("documents");
            builder.HasKey(x => x.CaseDocumentID);

            builder.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.StoragePath).HasMaxLength(400).IsRequired();
            builder.Property(x => x.Sha256).IsRequired();
            builder.Property(x => x.DocType).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.PdfProducer).HasMaxLength(200);
            builder.Property(x => x.PdfCreator).HasMaxLength(200);
            builder.Property(x => x.FontSet).HasMaxLength(1000);
            builder.Property(x => x.ClaimedIssuer).HasMaxLength(200);
            builder.Property(x => x.ExtractStatus).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasOne(x => x.LoanApplication).WithMany(a => a.Documents)
                   .HasForeignKey(x => x.LoanApplicationID).OnDelete(DeleteBehavior.Restrict);

            // The same file in two applications is the cheapest cross-case link there is
            builder.HasIndex(x => x.Sha256);
        }
    }

    public class StatementRowConfiguration : IEntityTypeConfiguration<StatementRow>
    {
        public void Configure(EntityTypeBuilder<StatementRow> builder)
        {
            builder.ToTable("statement_rows");
            builder.HasKey(x => x.StatementRowID);

            builder.Property(x => x.Description).HasMaxLength(400).IsRequired();
            builder.Property(x => x.Debit).HasPrecision(14, 2);
            builder.Property(x => x.Credit).HasPrecision(14, 2);
            builder.Property(x => x.Balance).HasPrecision(14, 2);
            builder.Property(x => x.CounterpartyName).HasMaxLength(200);
            builder.Property(x => x.CounterpartyAcct).HasMaxLength(50);
            builder.Property(x => x.CounterpartyClass).HasMaxLength(20);
            builder.Property(x => x.Box).HasMaxLength(40);

            builder.HasOne<CaseDocument>().WithMany()
                   .HasForeignKey(x => x.CaseDocumentID).OnDelete(DeleteBehavior.Cascade);

            // Balance continuity walks rows in order: LAG() OVER (PARTITION BY doc ORDER BY row_no)
            builder.HasIndex(x => new { x.CaseDocumentID, x.RowNo }).IsUnique();
        }
    }

    public class PageSummaryConfiguration : IEntityTypeConfiguration<PageSummary>
    {
        public void Configure(EntityTypeBuilder<PageSummary> builder)
        {
            builder.ToTable("page_summaries");
            builder.HasKey(x => new { x.CaseDocumentID, x.PageNo });

            builder.Property(x => x.OpeningBalance).HasPrecision(14, 2);
            builder.Property(x => x.ClosingBalance).HasPrecision(14, 2);
            builder.Property(x => x.StatedTotalDebit).HasPrecision(14, 2);
            builder.Property(x => x.StatedTotalCredit).HasPrecision(14, 2);

            builder.HasOne<CaseDocument>().WithMany()
                   .HasForeignKey(x => x.CaseDocumentID).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class FindingConfiguration : IEntityTypeConfiguration<Finding>
    {
        public void Configure(EntityTypeBuilder<Finding> builder)
        {
            builder.ToTable("findings");
            builder.HasKey(x => x.FindingID);

            builder.Property(x => x.Type).HasMaxLength(40).IsRequired();
            builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(25);
            builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
            builder.Property(x => x.Explanation).IsRequired();
            builder.Property(x => x.DetectedBy).HasConversion<string>().HasMaxLength(15);
            builder.Property(x => x.DedupKey).HasMaxLength(200).IsRequired();
            builder.Property(x => x.ReviewStatus).HasConversion<string>().HasMaxLength(12);
            builder.Property(x => x.ReviewedBy).HasMaxLength(100);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasOne<LoanApplication>().WithMany()
                   .HasForeignKey(x => x.LoanApplicationID).OnDelete(DeleteBehavior.Restrict);

            // One finding per (application, what it is about) — re-runs replace, never duplicate
            builder.HasIndex(x => new { x.LoanApplicationID, x.DedupKey }).IsUnique();
        }
    }

    public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
    {
        public void Configure(EntityTypeBuilder<Evidence> builder)
        {
            builder.ToTable("evidence");
            builder.HasKey(x => x.EvidenceID);

            builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.Box).HasMaxLength(40);
            builder.Property(x => x.Note).HasMaxLength(400);

            builder.HasOne<Finding>().WithMany(f => f.Evidence)
                   .HasForeignKey(x => x.FindingID).OnDelete(DeleteBehavior.Cascade);
        }
    }

    public class AiDropLogConfiguration : IEntityTypeConfiguration<AiDropLog>
    {
        public void Configure(EntityTypeBuilder<AiDropLog> builder)
        {
            builder.ToTable("ai_drop_log");
            builder.HasKey(x => x.AiDropLogID);

            builder.Property(x => x.RawFinding).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Reason).HasMaxLength(400).IsRequired();
            builder.Property(x => x.Model).HasMaxLength(60).IsRequired();

            builder.HasOne<LoanApplication>().WithMany()
                   .HasForeignKey(x => x.LoanApplicationID).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.LoanApplicationID, x.At });
        }
    }

    public class DocFieldConfiguration : IEntityTypeConfiguration<DocField>
    {
        public void Configure(EntityTypeBuilder<DocField> builder)
        {
            builder.ToTable("doc_fields");
            builder.HasKey(x => x.DocFieldID);

            builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Value).HasMaxLength(400).IsRequired();
            builder.Property(x => x.NumValue).HasPrecision(14, 2);
            builder.Property(x => x.Box).HasMaxLength(40);

            builder.HasOne<CaseDocument>().WithMany()
                   .HasForeignKey(x => x.CaseDocumentID).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(x => new { x.CaseDocumentID, x.Name });
        }
    }

    /// <summary>AI credit decisions — Doc/DB_SCRIPTS.md, 2026-10-16.</summary>
    public class CreditDecisionConfiguration : IEntityTypeConfiguration<CreditDecision>
    {
        public void Configure(EntityTypeBuilder<CreditDecision> builder)
        {
            builder.ToTable("credit_decisions");
            builder.HasKey(x => x.CreditDecisionID);

            builder.Property(x => x.Uan).HasMaxLength(30).IsRequired();
            builder.Property(x => x.Product).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.RequestedAmount).HasPrecision(14, 2);
            builder.Property(x => x.RatePct).HasPrecision(5, 2);
            builder.Property(x => x.Capacity).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(10);
            builder.Property(x => x.RecommendedAmount).HasPrecision(14, 2);
            builder.Property(x => x.Reasons).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Conditions).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Dropped).HasColumnType("jsonb").IsRequired();
            builder.Property(x => x.Model).HasMaxLength(60).IsRequired();
            builder.Property(x => x.AcceptedAmount).HasPrecision(14, 2);
            builder.Property(x => x.AcceptedBy).HasMaxLength(100);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasOne<LoanApplication>().WithMany()
                   .HasForeignKey(x => x.LoanApplicationID).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.LoanApplicationID);
        }
    }
}
