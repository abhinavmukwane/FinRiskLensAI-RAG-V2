using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CaseSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "application_id",
                table: "rag_query_log",
                newName: "loan_application_id");

            migrationBuilder.RenameIndex(
                name: "ix_rag_query_log_application_id",
                table: "rag_query_log",
                newName: "ix_rag_query_log_loan_application_id");

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    loan_application_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    applicant_ref = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    uan = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    declared_income = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    declared_employer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    declared_emis = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    submitted_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_applications", x => x.loan_application_id);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    case_document_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    loan_application_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    doc_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: true),
                    pdf_producer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pdf_creator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pdf_created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    pdf_modified_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    font_set = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    claimed_issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    layout_text = table.Column<string>(type: "text", nullable: true),
                    extract_status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    extract_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documents", x => x.case_document_id);
                    table.ForeignKey(
                        name: "fk_documents_loan_applications_loan_application_id",
                        column: x => x.loan_application_id,
                        principalTable: "applications",
                        principalColumn: "loan_application_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "doc_fields",
                columns: table => new
                {
                    doc_field_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    case_document_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    num_value = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    box = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_doc_fields", x => x.doc_field_id);
                    table.ForeignKey(
                        name: "fk_doc_fields_documents_case_document_id",
                        column: x => x.case_document_id,
                        principalTable: "documents",
                        principalColumn: "case_document_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "page_summaries",
                columns: table => new
                {
                    case_document_id = table.Column<int>(type: "integer", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    opening_balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    closing_balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    stated_total_debit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    stated_total_credit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_page_summaries", x => new { x.case_document_id, x.page_no });
                    table.ForeignKey(
                        name: "fk_page_summaries_documents_case_document_id",
                        column: x => x.case_document_id,
                        principalTable: "documents",
                        principalColumn: "case_document_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "statement_rows",
                columns: table => new
                {
                    statement_row_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    case_document_id = table.Column<int>(type: "integer", nullable: false),
                    page_no = table.Column<int>(type: "integer", nullable: false),
                    row_no = table.Column<int>(type: "integer", nullable: false),
                    txn_date = table.Column<DateOnly>(type: "date", nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    debit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    credit = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    balance = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    counterparty_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    counterparty_acct = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    counterparty_class = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    box = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_statement_rows", x => x.statement_row_id);
                    table.ForeignKey(
                        name: "fk_statement_rows_documents_case_document_id",
                        column: x => x.case_document_id,
                        principalTable: "documents",
                        principalColumn: "case_document_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_submitted_at",
                table: "applications",
                column: "submitted_at");

            migrationBuilder.CreateIndex(
                name: "ix_applications_uan",
                table: "applications",
                column: "uan");

            migrationBuilder.CreateIndex(
                name: "ix_doc_fields_case_document_id_name",
                table: "doc_fields",
                columns: new[] { "case_document_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_documents_loan_application_id",
                table: "documents",
                column: "loan_application_id");

            migrationBuilder.CreateIndex(
                name: "ix_documents_sha256",
                table: "documents",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_statement_rows_case_document_id_row_no",
                table: "statement_rows",
                columns: new[] { "case_document_id", "row_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doc_fields");

            migrationBuilder.DropTable(
                name: "page_summaries");

            migrationBuilder.DropTable(
                name: "statement_rows");

            migrationBuilder.DropTable(
                name: "documents");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.RenameColumn(
                name: "loan_application_id",
                table: "rag_query_log",
                newName: "application_id");

            migrationBuilder.RenameIndex(
                name: "ix_rag_query_log_loan_application_id",
                table: "rag_query_log",
                newName: "ix_rag_query_log_application_id");
        }
    }
}
