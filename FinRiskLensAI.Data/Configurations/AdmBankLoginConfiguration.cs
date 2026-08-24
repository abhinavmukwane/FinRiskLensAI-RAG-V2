using FinRiskLensAI.Core.Models.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinRiskLensAI.Data.Configurations
{
    public class AdmBankLoginConfiguration : IEntityTypeConfiguration<AdmBankLogin>
    {
        public void Configure(EntityTypeBuilder<AdmBankLogin> builder)
        {
            builder.ToTable("ADM_BankLogin");

            builder.HasKey(x => x.AdmBankLoginID);
            builder.Property(x => x.AdmBankLoginID).UseIdentityColumn();

            builder.Property(x => x.UserId).IsRequired().HasMaxLength(50);
            builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);

            builder.Property(x => x.FullName).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Email).HasMaxLength(256);
            builder.Property(x => x.MobileNumber).HasMaxLength(15);
            builder.Property(x => x.Designation).HasMaxLength(100);

            builder.Property(x => x.IfscCode).IsRequired().HasMaxLength(11);
            builder.Property(x => x.BankName).HasMaxLength(150);
            builder.Property(x => x.BranchCode).HasMaxLength(20);
            builder.Property(x => x.BranchName).HasMaxLength(150);
            builder.Property(x => x.BranchAddress).HasMaxLength(300);
            builder.Property(x => x.City).HasMaxLength(100);
            builder.Property(x => x.State).HasMaxLength(100);
            builder.Property(x => x.Pin).HasMaxLength(6);

            builder.Property(x => x.Role).IsRequired().HasMaxLength(50);
            builder.Property(x => x.CreatedBy).HasMaxLength(100);
            builder.Property(x => x.UpdatedBy).HasMaxLength(100);

            builder.HasIndex(x => x.UserId).IsUnique();
            builder.HasIndex(x => x.IfscCode);

            // Seed a default bank user (password: Bank@123 — change after first login).
            // Fixed values keep the generated migration deterministic.
            builder.HasData(new
            {
                AdmBankLoginID = 1,
                UserId = "bankadmin",
                PasswordHash = "AQAAAAIAAYagAAAAEIPBkSkagDEeRELmgDgCxJZinAKg/KQmO6hPeDdLe8u4haJi+IM8JF0dUZEbZgkqkA==",
                FullName = "IDBI Bank Administrator",
                Email = "bankadmin@idbi.co.in",
                MobileNumber = "9000000001",
                Designation = "Credit Manager",
                IfscCode = "IBKL0000472",
                BankName = "IDBI Bank",
                BranchCode = "000472",
                BranchName = "Sitabuldi, Nagpur",
                BranchAddress = "IDBI Bank Ltd, Sitabuldi Main Road, Nagpur",
                City = "Nagpur",
                State = "Maharashtra",
                Pin = "440012",
                Role = "BankAdmin",
                IsActive = true,
                FailedLoginCount = 0,
                CreatedAt = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 24, 0, 0, 0, DateTimeKind.Utc),
                CreatedBy = "seed"
            });
        }
    }
}
