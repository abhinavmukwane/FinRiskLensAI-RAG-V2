using FinRiskLensAI.Core.Models.Onboarding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class MsmeEnquiryConfiguration : IEntityTypeConfiguration<MsmeEnquiry>
    {
        public void Configure(EntityTypeBuilder<MsmeEnquiry> builder)
        {
            builder.ToTable("t_MsmeEnquiry");

            builder.HasKey(x => x.MsmeEnquiryID);

            builder.Property(x => x.ClientId).IsRequired().HasMaxLength(100);
            builder.Property(x => x.Uan).IsRequired().HasMaxLength(30);
            builder.Property(x => x.CertificateUrl).HasMaxLength(500);

            builder.Property(x => x.NameOfEnterprise).IsRequired().HasMaxLength(300);
            builder.Property(x => x.MajorActivity).HasMaxLength(100);
            builder.Property(x => x.SocialCategory).HasMaxLength(50);
            builder.Property(x => x.DicName).HasMaxLength(100);
            builder.Property(x => x.State).HasMaxLength(100);
            builder.Property(x => x.Flat).HasMaxLength(200);
            builder.Property(x => x.NameOfBuilding).HasMaxLength(200);
            builder.Property(x => x.Road).HasMaxLength(200);
            builder.Property(x => x.Village).HasMaxLength(200);
            builder.Property(x => x.Block).HasMaxLength(200);
            builder.Property(x => x.City).HasMaxLength(100);
            builder.Property(x => x.Pin).HasMaxLength(10);
            builder.Property(x => x.MobileNumber).HasMaxLength(20);
            builder.Property(x => x.Email).HasMaxLength(256);
            builder.Property(x => x.OrganizationType).HasMaxLength(100);
            builder.Property(x => x.Gender).HasMaxLength(20);
            builder.Property(x => x.MsmeDfo).HasMaxLength(100);

            builder.Property(x => x.GstinNumber).HasMaxLength(100);
            builder.Property(x => x.PanNumber).HasMaxLength(100);

            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            // Full raw API response — no length cap
            builder.Property(x => x.Payload).IsRequired();
            builder.Property(x => x.IsRegister).IsRequired().HasDefaultValue(false);

            // UAN is the cache lookup key: one enquiry row per Udyam number
            builder.HasIndex(x => x.Uan).IsUnique();

            builder.HasMany(x => x.Locations)
                   .WithOne(x => x.MsmeEnquiry)
                   .HasForeignKey(x => x.MsmeEnquiryID)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.NicCodes)
                   .WithOne(x => x.MsmeEnquiry)
                   .HasForeignKey(x => x.MsmeEnquiryID)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
