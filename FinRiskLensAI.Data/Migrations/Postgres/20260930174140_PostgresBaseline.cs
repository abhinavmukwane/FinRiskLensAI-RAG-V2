using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace FinRiskLensAI.Data.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PostgresBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "ADM_BankLogin",
                columns: table => new
                {
                    adm_bank_login_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    mobile_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    designation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ifsc_code = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    bank_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    branch_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    branch_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    branch_address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adm_bank_login", x => x.adm_bank_login_id);
                });

            migrationBuilder.CreateTable(
                name: "ADM_Login",
                columns: table => new
                {
                    adm_login_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_adm_login", x => x.adm_login_id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_docs",
                columns: table => new
                {
                    knowledge_doc_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    corpus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    source_uri = table.Column<string>(type: "text", nullable: true),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: false),
                    version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    lender_id = table.Column<int>(type: "integer", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_docs", x => x.knowledge_doc_id);
                });

            migrationBuilder.CreateTable(
                name: "m_StaticResponces",
                columns: table => new
                {
                    static_responces_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    udyam_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    udyam_number_responce = table.Column<string>(type: "text", nullable: true),
                    pan_number_responce = table.Column<string>(type: "text", nullable: true),
                    gst_number_responce = table.Column<string>(type: "text", nullable: true),
                    gst2b_responce = table.Column<string>(type: "text", nullable: true),
                    gst3b_responce = table.Column<string>(type: "text", nullable: true),
                    itr_number_responce = table.Column<string>(type: "text", nullable: true),
                    ip_responce = table.Column<string>(type: "text", nullable: true),
                    mca_responce = table.Column<string>(type: "text", nullable: true),
                    din_responce = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_m_static_responces", x => x.static_responces_id);
                });

            migrationBuilder.CreateTable(
                name: "rag_query_log",
                columns: table => new
                {
                    rag_query_log_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    application_id = table.Column<int>(type: "integer", nullable: true),
                    actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    question = table.Column<string>(type: "text", nullable: false),
                    corpora = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    retrieved_ids = table.Column<string>(type: "jsonb", nullable: false),
                    answer = table.Column<string>(type: "text", nullable: true),
                    grounded = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    stripped_claims = table.Column<int>(type: "integer", nullable: false),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rag_query_log", x => x.rag_query_log_id);
                });

            migrationBuilder.CreateTable(
                name: "t_AAConsentRequest",
                columns: table => new
                {
                    consent_req_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    cust_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ts = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    channel_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    encrypted_request = table.Column<string>(type: "text", nullable: false),
                    request_date = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    encrypted_fiu_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    consent_handle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_aa_consent_request", x => x.consent_req_id);
                });

            migrationBuilder.CreateTable(
                name: "t_AccAggreToken",
                columns: table => new
                {
                    token_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ts = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_acc_aggre_token", x => x.token_id);
                });

            migrationBuilder.CreateTable(
                name: "t_GSTR2And3BResponce",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    udyam_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    gstin_number = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    filing_period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    _2BRequestData = table.Column<string>(name: "2BRequestData", type: "text", nullable: false),
                    _3BRequestData = table.Column<string>(name: "3BRequestData", type: "text", nullable: false),
                    _2BResponseData = table.Column<string>(name: "2BResponseData", type: "text", nullable: false),
                    _3BResponseData = table.Column<string>(name: "3BResponseData", type: "text", nullable: false),
                    created_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_gstr2and3b_responce", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "t_LoanCasePush",
                columns: table => new
                {
                    loan_case_push_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    uan = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    enterprise_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    case_reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    http_status = table.Column<int>(type: "integer", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: false),
                    response_body = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    pushed_by_login_id = table.Column<int>(type: "integer", nullable: false),
                    pushed_by_user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    pushed_by_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    pushed_from_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    pushed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_loan_case_push", x => x.loan_case_push_id);
                });

            migrationBuilder.CreateTable(
                name: "t_MsmeEnquiry",
                columns: table => new
                {
                    msme_enquiry_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    uan = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    certificate_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    name_of_enterprise = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    major_activity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    social_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    date_of_commencement = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    dic_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    applied_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    flat = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_of_building = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    road = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    village = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    block = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    mobile_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    organization_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gender = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    date_of_incorporation = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    msme_dfo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registration_date = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    gstin_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pan_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_register = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_msme_enquiry", x => x.msme_enquiry_id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    knowledge_chunk_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    knowledge_doc_id = table.Column<int>(type: "integer", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    breadcrumb = table.Column<string>(type: "text", nullable: true),
                    chunk_text = table.Column<string>(type: "text", nullable: false),
                    token_count = table.Column<int>(type: "integer", nullable: false),
                    source_finding_id = table.Column<int>(type: "integer", nullable: true),
                    source_ring_id = table.Column<int>(type: "integer", nullable: true),
                    is_current = table.Column<bool>(type: "boolean", nullable: false),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('english', coalesce(breadcrumb, '') || ' ' || chunk_text)", stored: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_chunks", x => x.knowledge_chunk_id);
                    table.ForeignKey(
                        name: "fk_knowledge_chunks_knowledge_docs_knowledge_doc_id",
                        column: x => x.knowledge_doc_id,
                        principalTable: "knowledge_docs",
                        principalColumn: "knowledge_doc_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "t_MsmeLocations",
                columns: table => new
                {
                    msme_location_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    msme_enquiry_id = table.Column<int>(type: "integer", nullable: false),
                    unit_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    building = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    village = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    road = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pin = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    state = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    district = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_msme_locations", x => x.msme_location_id);
                    table.ForeignKey(
                        name: "fk_t_msme_locations_t_msme_enquiry_msme_enquiry_id",
                        column: x => x.msme_enquiry_id,
                        principalTable: "t_MsmeEnquiry",
                        principalColumn: "msme_enquiry_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "t_MsmeNicCodes",
                columns: table => new
                {
                    msme_nic_code_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    msme_enquiry_id = table.Column<int>(type: "integer", nullable: false),
                    nic2digit = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nic4digit = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    nic5digit = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    added_on = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_msme_nic_codes", x => x.msme_nic_code_id);
                    table.ForeignKey(
                        name: "fk_t_msme_nic_codes_t_msme_enquiry_msme_enquiry_id",
                        column: x => x.msme_enquiry_id,
                        principalTable: "t_MsmeEnquiry",
                        principalColumn: "msme_enquiry_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "t_UserRegistration",
                columns: table => new
                {
                    user_registration_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mobile_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    udyam_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gstin_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    pan_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    msme_enquiry_id = table.Column<int>(type: "integer", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_user_registration", x => x.user_registration_id);
                    table.ForeignKey(
                        name: "fk_t_user_registration_t_msme_enquiry_msme_enquiry_id",
                        column: x => x.msme_enquiry_id,
                        principalTable: "t_MsmeEnquiry",
                        principalColumn: "msme_enquiry_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chunk_embeddings",
                columns: table => new
                {
                    chunk_embedding_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    knowledge_chunk_id = table.Column<long>(type: "bigint", nullable: false),
                    model_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dimensions = table.Column<int>(type: "integer", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chunk_embeddings", x => x.chunk_embedding_id);
                    table.ForeignKey(
                        name: "fk_chunk_embeddings_knowledge_chunks_knowledge_chunk_id",
                        column: x => x.knowledge_chunk_id,
                        principalTable: "knowledge_chunks",
                        principalColumn: "knowledge_chunk_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "t_UserOtp",
                columns: table => new
                {
                    user_otp_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_registration_id = table.Column<int>(type: "integer", nullable: true),
                    msme_enquiry_id = table.Column<int>(type: "integer", nullable: true),
                    mobile_number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    otp = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_t_user_otp", x => x.user_otp_id);
                    table.ForeignKey(
                        name: "fk_t_user_otp_t_msme_enquiry_msme_enquiry_id",
                        column: x => x.msme_enquiry_id,
                        principalTable: "t_MsmeEnquiry",
                        principalColumn: "msme_enquiry_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_t_user_otp_t_user_registration_user_registration_id",
                        column: x => x.user_registration_id,
                        principalTable: "t_UserRegistration",
                        principalColumn: "user_registration_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "ADM_BankLogin",
                columns: new[] { "adm_bank_login_id", "bank_name", "branch_address", "branch_code", "branch_name", "city", "created_at", "created_by", "designation", "email", "failed_login_count", "full_name", "ifsc_code", "is_active", "last_login_at", "mobile_number", "password_hash", "pin", "role", "state", "updated_at", "updated_by", "user_id" },
                values: new object[] { 1, "IDBI Bank", "IDBI Bank Ltd, Sitabuldi Main Road, Nagpur", "000472", "Sitabuldi, Nagpur", "Nagpur", new DateTime(2026, 8, 24, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "Credit Manager", "bankadmin@idbi.co.in", 0, "IDBI Bank Administrator", "IBKL0000472", true, null, "9000000001", "AQAAAAIAAYagAAAAEIPBkSkagDEeRELmgDgCxJZinAKg/KQmO6hPeDdLe8u4haJi+IM8JF0dUZEbZgkqkA==", "440012", "BankAdmin", "Maharashtra", new DateTime(2026, 8, 24, 0, 0, 0, 0, DateTimeKind.Utc), null, "bankadmin" });

            migrationBuilder.InsertData(
                table: "ADM_Login",
                columns: new[] { "adm_login_id", "created_at", "created_by", "email", "is_active", "last_login_at", "password_hash", "role", "updated_at", "updated_by", "username" },
                values: new object[] { 1, new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seed", "admin@finrisklens.ai", true, null, "AQAAAAIAAYagAAAAECqQCLDS1XMpIauPSCY8GXjyewL5WfquYrxarGU82tSJ8a+XzMzhzkdWS+Dfu1hVcA==", "SuperAdmin", new DateTime(2026, 7, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "admin" });

            migrationBuilder.CreateIndex(
                name: "ix_adm_bank_login_ifsc_code",
                table: "ADM_BankLogin",
                column: "ifsc_code");

            migrationBuilder.CreateIndex(
                name: "ix_adm_bank_login_user_id",
                table: "ADM_BankLogin",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_adm_login_email",
                table: "ADM_Login",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_adm_login_username",
                table: "ADM_Login",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embeddings_embedding",
                table: "chunk_embeddings",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" })
                .Annotation("Npgsql:StorageParameter:ef_construction", 64)
                .Annotation("Npgsql:StorageParameter:m", 16);

            migrationBuilder.CreateIndex(
                name: "ix_chunk_embeddings_knowledge_chunk_id_model_id",
                table: "chunk_embeddings",
                columns: new[] { "knowledge_chunk_id", "model_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_knowledge_doc_id_ordinal",
                table: "knowledge_chunks",
                columns: new[] { "knowledge_doc_id", "ordinal" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_search_vector",
                table: "knowledge_chunks",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_docs_corpus_is_current",
                table: "knowledge_docs",
                columns: new[] { "corpus", "is_current" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_docs_sha256",
                table: "knowledge_docs",
                column: "sha256");

            migrationBuilder.CreateIndex(
                name: "ix_m_static_responces_udyam_number",
                table: "m_StaticResponces",
                column: "udyam_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rag_query_log_application_id",
                table: "rag_query_log",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_aa_consent_request_consent_handle",
                table: "t_AAConsentRequest",
                column: "consent_handle");

            migrationBuilder.CreateIndex(
                name: "ix_t_aa_consent_request_transaction_id",
                table: "t_AAConsentRequest",
                column: "transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_aa_consent_request_uan",
                table: "t_AAConsentRequest",
                column: "uan");

            migrationBuilder.CreateIndex(
                name: "ix_t_gstr2and3b_responce_udyam_number",
                table: "t_GSTR2And3BResponce",
                column: "udyam_number");

            migrationBuilder.CreateIndex(
                name: "IX_LoanCasePush_Uan_Channel_PushedAt",
                table: "t_LoanCasePush",
                columns: new[] { "uan", "channel", "pushed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_t_msme_enquiry_uan",
                table: "t_MsmeEnquiry",
                column: "uan",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_t_msme_locations_msme_enquiry_id",
                table: "t_MsmeLocations",
                column: "msme_enquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_msme_nic_codes_msme_enquiry_id",
                table: "t_MsmeNicCodes",
                column: "msme_enquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_otp_msme_enquiry_id",
                table: "t_UserOtp",
                column: "msme_enquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_otp_user_registration_id",
                table: "t_UserOtp",
                column: "user_registration_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_registration_gstin_number",
                table: "t_UserRegistration",
                column: "gstin_number");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_registration_mobile_number",
                table: "t_UserRegistration",
                column: "mobile_number");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_registration_msme_enquiry_id",
                table: "t_UserRegistration",
                column: "msme_enquiry_id");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_registration_pan_number",
                table: "t_UserRegistration",
                column: "pan_number");

            migrationBuilder.CreateIndex(
                name: "ix_t_user_registration_udyam_number",
                table: "t_UserRegistration",
                column: "udyam_number");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ADM_BankLogin");

            migrationBuilder.DropTable(
                name: "ADM_Login");

            migrationBuilder.DropTable(
                name: "chunk_embeddings");

            migrationBuilder.DropTable(
                name: "m_StaticResponces");

            migrationBuilder.DropTable(
                name: "rag_query_log");

            migrationBuilder.DropTable(
                name: "t_AAConsentRequest");

            migrationBuilder.DropTable(
                name: "t_AccAggreToken");

            migrationBuilder.DropTable(
                name: "t_GSTR2And3BResponce");

            migrationBuilder.DropTable(
                name: "t_LoanCasePush");

            migrationBuilder.DropTable(
                name: "t_MsmeLocations");

            migrationBuilder.DropTable(
                name: "t_MsmeNicCodes");

            migrationBuilder.DropTable(
                name: "t_UserOtp");

            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "t_UserRegistration");

            migrationBuilder.DropTable(
                name: "knowledge_docs");

            migrationBuilder.DropTable(
                name: "t_MsmeEnquiry");
        }
    }
}
