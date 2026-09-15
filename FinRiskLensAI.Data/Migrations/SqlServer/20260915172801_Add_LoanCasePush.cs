using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class Add_LoanCasePush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "t_LoanCasePush",
                columns: table => new
                {
                    LoanCasePushID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Uan = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    CaseReference = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Endpoint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HttpStatus = table.Column<int>(type: "int", nullable: true),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ScoreAtPush = table.Column<double>(type: "float", nullable: false),
                    BandAtPush = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EligibilityAtPush = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ScoreComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PushedByLoginId = table.Column<int>(type: "int", nullable: false),
                    PushedByUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PushedByName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    PushedFromIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PushedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_t_LoanCasePush", x => x.LoanCasePushID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanCasePush_Uan_Channel_PushedAt",
                table: "t_LoanCasePush",
                columns: new[] { "Uan", "Channel", "PushedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "t_LoanCasePush");
        }
    }
}
