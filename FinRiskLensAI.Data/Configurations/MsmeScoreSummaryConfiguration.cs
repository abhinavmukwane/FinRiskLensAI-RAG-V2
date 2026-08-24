using FinRiskLensAI.Core.Models.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class MsmeScoreSummaryConfiguration : IEntityTypeConfiguration<MsmeScoreSummary>
    {
        public void Configure(EntityTypeBuilder<MsmeScoreSummary> builder)
        {
            builder.ToTable("t_MsmeScoreSummary");

            builder.HasKey(x => x.MsmeScoreSummaryID);
            builder.Property(x => x.MsmeScoreSummaryID).UseIdentityColumn();

            builder.Property(x => x.Uan).IsRequired().HasMaxLength(25);
            builder.Property(x => x.ScoreBand).IsRequired().HasMaxLength(20);
            builder.Property(x => x.TopProductName).HasMaxLength(150);
            builder.Property(x => x.TopProductScheme).HasMaxLength(50);
            builder.Property(x => x.ModelVersion).HasMaxLength(50);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Money columns — 18,2 is enough; the context-wide default is 18,4.
            builder.Property(x => x.WorkingCapitalLimit).HasPrecision(18, 2);
            builder.Property(x => x.TermLoanCapacity).HasPrecision(18, 2);
            builder.Property(x => x.TotalIndicativeEligibility).HasPrecision(18, 2);
            builder.Property(x => x.AnnualTurnover).HasPrecision(18, 2);
            builder.Property(x => x.MonthlySurplus).HasPrecision(18, 2);

            // One row per MSME — the upsert keys on this.
            builder.HasIndex(x => x.Uan).IsUnique();
            // Portfolio dashboard filters and sorts on these.
            builder.HasIndex(x => x.ScoreBand);
            builder.HasIndex(x => x.OverallScore);
        }
    }
}
