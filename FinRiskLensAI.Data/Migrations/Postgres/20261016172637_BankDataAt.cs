using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class BankDataAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "bank_data_at",
                table: "applications",
                type: "timestamp without time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bank_data_at",
                table: "applications");
        }
    }
}
