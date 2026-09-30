using FinRiskLensAI.Core.Models.GST;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class GSTR2And3BResponceConfiguration : IEntityTypeConfiguration<GSTR2And3BResponceModel>
    {
        public void Configure(EntityTypeBuilder<GSTR2And3BResponceModel> builder)
        {
            builder.ToTable("t_GSTR2And3BResponce");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.Property(x => x.UdyamNumber)
                   .HasMaxLength(15)
                   .IsRequired();

            builder.Property(x => x.GSTINNumber)
                   .HasMaxLength(15);

            builder.Property(x => x.FilingPeriod)
                   .HasMaxLength(10);

            // C# property names can't start with a digit — map to the 2B*/3B* columns.
            builder.Property(x => x.GSTR2BRequestData)
                   .HasColumnName("2BRequestData")
                   .IsRequired();

            builder.Property(x => x.GSTR3BRequestData)
                   .HasColumnName("3BRequestData")
                   .IsRequired();

            builder.Property(x => x.GSTR2BResponseData)
                   .HasColumnName("2BResponseData")
                   .IsRequired();

            builder.Property(x => x.GSTR3BResponseData)
                   .HasColumnName("3BResponseData")
                   .IsRequired();

            builder.Property(x => x.CreatedDate)
                   .HasDefaultValueSql("now()");

            builder.Property(x => x.CreatedBy);

            // Latest-record lookups are by UdyamNumber.
            builder.HasIndex(x => x.UdyamNumber);
        }
    }
}
