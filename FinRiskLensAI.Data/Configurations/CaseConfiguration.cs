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
}
