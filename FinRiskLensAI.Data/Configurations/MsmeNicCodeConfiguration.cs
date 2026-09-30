using FinRiskLensAI.Core.Models.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class MsmeNicCodeConfiguration : IEntityTypeConfiguration<MsmeNicCode>
    {
        public void Configure(EntityTypeBuilder<MsmeNicCode> builder)
        {
            builder.ToTable("t_MsmeNicCodes");

            builder.HasKey(x => x.MsmeNicCodeID);

            // Values arrive as "code - description" strings, e.g.
            // "62011 - Writing, modifying, testing of computer program..."
            builder.Property(x => x.Nic2Digit).HasMaxLength(200);
            builder.Property(x => x.Nic4Digit).HasMaxLength(300);
            builder.Property(x => x.Nic5Digit).HasMaxLength(500);
            builder.Property(x => x.ActivityType).HasMaxLength(50);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasIndex(x => x.MsmeEnquiryID);
        }
    }
}
