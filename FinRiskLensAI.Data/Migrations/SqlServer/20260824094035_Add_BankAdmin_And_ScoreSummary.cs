using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Add_BankAdmin_And_ScoreSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ADM_BankLogin",
                columns: table => new
                {
                    AdmBankLoginID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MobileNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Designation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IfscCode = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BranchCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    BranchName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    BranchAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Pin = table.Column<string>(type: "nvarchar(6)", maxLength: 6, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedLoginCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ADM_BankLogin", x => x.AdmBankLoginID);
                });

            migrationBuilder.CreateTable(
                name: "t_MsmeScoreSummary",
                columns: table => new
                {
                    MsmeScoreSummaryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uan = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    OverallScore = table.Column<double>(type: "float", nullable: false),
                    ScoreBand = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HeuristicScore = table.Column<double>(type: "float", nullable: false),
                    MlCalibratedScore = table.Column<double>(type: "float", nullable: false),
                    CashflowTrendSlope = table.Column<double>(type: "float", nullable: false),
                    WorkingCapitalLimit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TermLoanCapacity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIndicativeEligibility = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualTurnover = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlySurplus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TopProductName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TopProductScheme = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsAnomalous = table.Column<bool>(type: "bit", nullable: false),
                    DimensionsExcludedCount = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_t_MsmeScoreSummary", x => x.MsmeScoreSummaryID);
                });

            migrationBuilder.InsertData(
                table: "ADM_BankLogin",
                columns: new[] { "AdmBankLoginID", "BankName", "BranchAddress", "BranchCode", "BranchName", "City", "CreatedAt", "CreatedBy", "Designation", "Email", "FailedLoginCount", "FullName", "IfscCode", "IsActive", "LastLoginAt", "MobileNumber", "PasswordHash", "Pin", "Role", "State", "UpdatedAt", "UpdatedBy", "UserId" },
                values: new object[] { 1, "IDBI Bank", "IDBI Bank Ltd, Sitabuldi Main Road, Nagpur", "000472", "Sitabuldi, Nagpur", "Nagpur", new DateTime(2026, 8, 24, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Credit Manager", "bankadmin@idbi.co.in", 0, "IDBI Bank Administrator", "IBKL0000472", true, null, "9000000001", "AQAAAAIAAYagAAAAEIPBkSkagDEeRELmgDgCxJZinAKg/KQmO6hPeDdLe8u4haJi+IM8JF0dUZEbZgkqkA==", "440012", "BankAdmin", "Maharashtra", new DateTime(2026, 8, 24, 0, 0, 0, 0, DateTimeKind.Utc), null, "bankadmin" });

            migrationBuilder.CreateIndex(
                name: "IX_ADM_BankLogin_IfscCode",
                table: "ADM_BankLogin",
                column: "IfscCode");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_BankLogin_UserId",
                table: "ADM_BankLogin",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_t_MsmeScoreSummary_OverallScore",
                table: "t_MsmeScoreSummary",
                column: "OverallScore");

            migrationBuilder.CreateIndex(
                name: "IX_t_MsmeScoreSummary_ScoreBand",
                table: "t_MsmeScoreSummary",
                column: "ScoreBand");

            migrationBuilder.CreateIndex(
                name: "IX_t_MsmeScoreSummary_Uan",
                table: "t_MsmeScoreSummary",
                column: "Uan",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_BankLogin");

            migrationBuilder.DropTable(
                name: "t_MsmeScoreSummary");
        }
    }
}
