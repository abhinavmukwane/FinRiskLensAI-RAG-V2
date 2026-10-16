using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CreditDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "credit_decisions",
                columns: table => new
                {
                    credit_decision_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    loan_application_id = table.Column<int>(type: "integer", nullable: false),
                    uan = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    product = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    tenure_months = table.Column<int>(type: "integer", nullable: false),
                    rate_pct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    capacity = table.Column<string>(type: "jsonb", nullable: false),
                    outcome = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    recommended_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    reasons = table.Column<string>(type: "jsonb", nullable: false),
                    conditions = table.Column<string>(type: "jsonb", nullable: false),
                    dropped = table.Column<string>(type: "jsonb", nullable: false),
                    model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    accepted_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    accepted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    accepted_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_credit_decisions", x => x.credit_decision_id);
                    table.ForeignKey(
                        name: "fk_credit_decisions_loan_applications_loan_application_id",
                        column: x => x.loan_application_id,
                        principalTable: "applications",
                        principalColumn: "loan_application_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_credit_decisions_loan_application_id",
                table: "credit_decisions",
                column: "loan_application_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "credit_decisions");
        }
    }
}
