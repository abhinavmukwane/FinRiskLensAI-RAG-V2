using FinRiskLensAI.Core.Models.Scoring;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class MsmeScoreHistoryConfiguration : IEntityTypeConfiguration<MsmeScoreHistory>
    {
        public void Configure(EntityTypeBuilder<MsmeScoreHistory> builder)
        {
            builder.ToTable("t_MsmeScoreHistory");

            builder.HasKey(x => x.MsmeScoreHistoryID);
            builder.Property(x => x.MsmeScoreHistoryID).UseIdentityColumn();

            builder.Property(x => x.Uan).IsRequired().HasMaxLength(25);
            builder.Property(x => x.ScoreBand).IsRequired().HasMaxLength(20);
            builder.Property(x => x.ModelVersion).HasMaxLength(50);
            builder.Property(x => x.Source).HasMaxLength(30);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.Property(x => x.AnnualTurnover).HasPrecision(18, 2);
            builder.Property(x => x.MonthlySurplus).HasPrecision(18, 2);
            builder.Property(x => x.TotalIndicativeEligibility).HasPrecision(18, 2);

            // The series read is always "this UAN, newest first" — one composite
            // index covers it. Deliberately NOT unique: the table is append-only,
            // and two runs in the same second are legitimate.
            builder.HasIndex(x => new { x.Uan, x.ComputedAt })
                   .HasDatabaseName("IX_MsmeScoreHistory_Uan_ComputedAt");
        }
    }
}
