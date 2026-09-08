using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Add_MsmeScoreHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "t_MsmeScoreHistory",
                columns: table => new
                {
                    MsmeScoreHistoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uan = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    OverallScore = table.Column<double>(type: "float", nullable: false),
                    ScoreBand = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HeuristicScore = table.Column<double>(type: "float", nullable: false),
                    MlCalibratedScore = table.Column<double>(type: "float", nullable: false),
                    CashflowTrendSlope = table.Column<double>(type: "float", nullable: false),
                    RevenueVitality = table.Column<double>(type: "float", nullable: false),
                    CashFlowHealth = table.Column<double>(type: "float", nullable: false),
                    TransactionTrust = table.Column<double>(type: "float", nullable: false),
                    ComplianceQuotient = table.Column<double>(type: "float", nullable: false),
                    BusinessStability = table.Column<double>(type: "float", nullable: false),
                    DebtServiceability = table.Column<double>(type: "float", nullable: false),
                    AnnualTurnover = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlySurplus = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalIndicativeEligibility = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsAnomalous = table.Column<bool>(type: "bit", nullable: false),
                    DimensionsExcludedCount = table.Column<int>(type: "int", nullable: false),
                    DataFilesUsed = table.Column<int>(type: "int", nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_t_MsmeScoreHistory", x => x.MsmeScoreHistoryID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MsmeScoreHistory_Uan_ComputedAt",
                table: "t_MsmeScoreHistory",
                columns: new[] { "Uan", "ComputedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "t_MsmeScoreHistory");
        }
    }
}
