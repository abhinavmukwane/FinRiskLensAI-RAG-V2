using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Findings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "findings",
                columns: table => new
                {
                    finding_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    loan_application_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    severity = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    explanation = table.Column<string>(type: "text", nullable: false),
                    recommended_action = table.Column<string>(type: "text", nullable: true),
                    detected_by = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    dedup_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    review_status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    review_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_findings", x => x.finding_id);
                    table.ForeignKey(
                        name: "fk_findings_loan_applications_loan_application_id",
                        column: x => x.loan_application_id,
                        principalTable: "applications",
                        principalColumn: "loan_application_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                columns: table => new
                {
                    evidence_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    finding_id = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ref_id = table.Column<long>(type: "bigint", nullable: true),
                    case_document_id = table.Column<int>(type: "integer", nullable: true),
                    page_no = table.Column<int>(type: "integer", nullable: true),
                    box = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    note = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence", x => x.evidence_id);
                    table.ForeignKey(
                        name: "fk_evidence_findings_finding_id",
                        column: x => x.finding_id,
                        principalTable: "findings",
                        principalColumn: "finding_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_finding_id",
                table: "evidence",
                column: "finding_id");

            migrationBuilder.CreateIndex(
                name: "ix_findings_loan_application_id_dedup_key",
                table: "findings",
                columns: new[] { "loan_application_id", "dedup_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence");

            migrationBuilder.DropTable(
                name: "findings");
        }
    }
}
