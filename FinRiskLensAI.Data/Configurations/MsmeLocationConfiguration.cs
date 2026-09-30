using FinRiskLensAI.Core.Models.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class MsmeLocationConfiguration : IEntityTypeConfiguration<MsmeLocation>
    {
        public void Configure(EntityTypeBuilder<MsmeLocation> builder)
        {
            builder.ToTable("t_MsmeLocations");

            builder.HasKey(x => x.MsmeLocationID);

            builder.Property(x => x.UnitName).HasMaxLength(300);
            builder.Property(x => x.Line1).HasMaxLength(200);
            builder.Property(x => x.Building).HasMaxLength(200);
            builder.Property(x => x.Village).HasMaxLength(200);
            builder.Property(x => x.Street).HasMaxLength(200);
            builder.Property(x => x.Road).HasMaxLength(200);
            builder.Property(x => x.City).HasMaxLength(100);
            builder.Property(x => x.Pin).HasMaxLength(10);
            builder.Property(x => x.State).HasMaxLength(100);
            builder.Property(x => x.District).HasMaxLength(100);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasIndex(x => x.MsmeEnquiryID);
        }
    }
}
