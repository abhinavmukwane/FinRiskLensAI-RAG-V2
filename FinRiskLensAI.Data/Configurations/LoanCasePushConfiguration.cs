using FinRiskLensAI.Core.Models.LoanCase;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class LoanCasePushConfiguration : IEntityTypeConfiguration<LoanCasePush>
    {
        public void Configure(EntityTypeBuilder<LoanCasePush> builder)
        {
            builder.ToTable("t_LoanCasePush");

            builder.HasKey(x => x.LoanCasePushID);
            builder.Property(x => x.LoanCasePushID).UseIdentityColumn();

            builder.Property(x => x.Uan).IsRequired().HasMaxLength(25);
            builder.Property(x => x.EnterpriseName).HasMaxLength(250);

            // Stored as their names, not ints — a DBA reading the table should see
            // "ULI" / "Simulated", and adding a channel must not renumber history.
            builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10).IsRequired();
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(12).IsRequired();

            builder.Property(x => x.CaseReference).HasMaxLength(80);
            builder.Property(x => x.Endpoint).HasMaxLength(500);
            builder.Property(x => x.Payload).IsRequired();          // nvarchar(max)
            builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
            builder.Property(x => x.BandAtPush).IsRequired().HasMaxLength(20);
            builder.Property(x => x.ModelVersion).HasMaxLength(50);
            builder.Property(x => x.EligibilityAtPush).HasPrecision(18, 2);
            builder.Property(x => x.PushedByUserId).IsRequired().HasMaxLength(100);
            builder.Property(x => x.PushedByName).HasMaxLength(150);
            builder.Property(x => x.PushedFromIp).HasMaxLength(64);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            // "Latest per (Uan, Channel)" is the only read pattern — one index serves
            // both the chips and the list badge. Deliberately not unique: append-only.
            builder.HasIndex(x => new { x.Uan, x.Channel, x.PushedAt })
                   .HasDatabaseName("IX_LoanCasePush_Uan_Channel_PushedAt");
        }
    }
}
