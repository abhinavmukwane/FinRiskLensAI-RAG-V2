# Database Scripts

Running log of every table created, altered or extended, with the SQL to apply
it by hand when a migration cannot be run.

**Convention:** each entity keeps an int identity primary key named
`<EntityName>ID`. Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedBy`,
`UpdatedBy`) come from `AuditableEntity` and are stamped automatically in
`ApplicationDbContext.SaveChangesAsync`.

**Database: PostgreSQL** (since 2026-09-30 — the RAG-V2 fork moved off SQL Server;
reasoning in `10_FRAUD_LENS_BUILD.md` §5). Development runs on **Neon**; the submission
target is Cloud SQL for PostgreSQL, a connection-string change. The connection string
lives in the git-ignored `appsettings.Development.json`
(`Database:ConnectionStrings:PostgreSQL`) and is deliberately not written down here.

**Schema:** application tables live in `public`. The EF migrations history table is
pinned at `cre."__EFMigrationsHistory"` (set in `PostgresSetup`).

**Naming:** a snake_case convention (`EFCore.NamingConventions`) applies to every column
and to tables without an explicit name. Tables kept from V1 keep their explicit names
(`"t_LoanCasePush"`, `"ADM_BankLogin"`) and need quoting in hand-written SQL; new tables
are named snake_case outright (`knowledge_chunks`) and need none.

**Timestamps:** `timestamp without time zone`, via Npgsql's legacy timestamp mode — the
codebase writes both `DateTime.Now` and `DateTime.UtcNow`, and modern Npgsql rejects one
or the other. Values are UTC by convention (`AuditableEntity` stamps `UtcNow`).

> Entries dated before 2026-09-30 record the **SQL Server era** and are kept as history.
> Their DDL is SQL Server syntax and does not apply to the current database.

**Add a new entry to this file whenever a table is created, a column is added,
or a column is altered — before running the migration.**

---

## Index

| Date | Change | Table |
|---|---|---|
| 2026-07-07 | Create — SQL-backed session store | `SessionCache` |
| 2026-08-24 | Create — bank portal login | `ADM_BankLogin` |
| 2026-08-24 | Create — SQL index over blob `result.json` | `t_MsmeScoreSummary` |
| 2026-09-08 | Create — append-only score series behind the trend chart | `t_MsmeScoreHistory` |
| 2026-09-15 | Create — append-only loan-case pushes to LOS / ULI / ONDC | `t_LoanCasePush` |
| 2026-09-15 | Alter — add `EnterpriseName` (frozen at push time) | `t_LoanCasePush` |
| **2026-09-30** | **PostgreSQL baseline** — 12 tables carried over, 4 knowledge tables created, score tables dropped | *(all)* |
| 2026-10-02 | Create — loan cases and their uploaded documents; rename `application_id` | `applications`, `documents`, `statement_rows`, `page_summaries`, `doc_fields`, `rag_query_log` |

---

## 2026-07-07 — `SessionCache` (new table)

Not an EF entity — this is the ASP.NET Core distributed session store
(`AddDistributedSqlServerCache`, registered before `AddSession` in `Program.cs`).

It exists because of a production bug: a heavy re-analyze (LightGBM / PCA training)
recycled the IIS app pool, the in-memory session was wiped, and the next request
redirected the user to the login page. A SQL-backed session survives an app-pool
recycle *and* a multi-instance farm. The `GetCurrentUser()` API is unchanged.

Run once per database. The table lives in the login's own schema — no `dbo` needed.

```sql
CREATE TABLE [FinRiskLensAI].[SessionCache](
    [Id]                         NVARCHAR(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
    [Value]                      VARBINARY(MAX)    NOT NULL,
    [ExpiresAtTime]              DATETIMEOFFSET(7) NOT NULL,
    [SlidingExpirationInSeconds] BIGINT            NULL,
    [AbsoluteExpiration]         DATETIMEOFFSET(7) NULL,
    CONSTRAINT [pk_Id] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE NONCLUSTERED INDEX [Index_ExpiresAtTime]
    ON [FinRiskLensAI].[SessionCache] ([ExpiresAtTime]);
GO
```

Equivalent to `dotnet sql-cache create "<conn>" FinRiskLensAI SessionCache`.

---

## 2026-08-24 — `ADM_BankLogin` (new table)

Bank-side portal user. Kept separate from `ADM_Login` because a bank user
carries branch identity (IFSC, branch code/name, address) that a platform admin
does not. Login is **User ID + password** — no OTP.

```sql
CREATE TABLE [FinRiskLensAI].[ADM_BankLogin]
(
    [AdmBankLoginID]   INT             IDENTITY(1,1) NOT NULL,

    -- Login credentials
    [UserId]           NVARCHAR(50)    NOT NULL,
    [PasswordHash]     NVARCHAR(500)   NOT NULL,   -- ASP.NET Identity V3 PBKDF2

    -- Person
    [FullName]         NVARCHAR(150)   NOT NULL,
    [Email]            NVARCHAR(256)   NULL,
    [MobileNumber]     NVARCHAR(15)    NULL,
    [Designation]      NVARCHAR(100)   NULL,

    -- Bank / branch identity
    [IfscCode]         NVARCHAR(11)    NOT NULL,
    [BankName]         NVARCHAR(150)   NULL,
    [BranchCode]       NVARCHAR(20)    NULL,
    [BranchName]       NVARCHAR(150)   NULL,
    [BranchAddress]    NVARCHAR(300)   NULL,
    [City]             NVARCHAR(100)   NULL,
    [State]            NVARCHAR(100)   NULL,
    [Pin]              NVARCHAR(6)     NULL,

    -- Access control
    [Role]             NVARCHAR(50)    NOT NULL,   -- BankAdmin / CreditOfficer / Viewer
    [IsActive]         BIT             NOT NULL,
    [LastLoginAt]      DATETIME2(7)    NULL,
    [FailedLoginCount] INT             NOT NULL,

    -- Audit (AuditableEntity)
    [CreatedAt]        DATETIME2(7)    NOT NULL,
    [UpdatedAt]        DATETIME2(7)    NOT NULL,
    [CreatedBy]        NVARCHAR(100)   NULL,
    [UpdatedBy]        NVARCHAR(100)   NULL,

    CONSTRAINT [PK_ADM_BankLogin] PRIMARY KEY CLUSTERED ([AdmBankLoginID] ASC)
);
GO

CREATE UNIQUE INDEX [IX_ADM_BankLogin_UserId]   ON [FinRiskLensAI].[ADM_BankLogin] ([UserId]);
CREATE        INDEX [IX_ADM_BankLogin_IfscCode] ON [FinRiskLensAI].[ADM_BankLogin] ([IfscCode]);
GO
```

### Seed row

Inserted by the EF configuration (`AdmBankLoginConfiguration.HasData`). Apply
manually only if you created the table by script instead of by migration.

```sql
SET IDENTITY_INSERT [FinRiskLensAI].[ADM_BankLogin] ON;

INSERT INTO [FinRiskLensAI].[ADM_BankLogin]
    ([AdmBankLoginID], [UserId], [PasswordHash], [FullName], [Email], [MobileNumber],
     [Designation], [IfscCode], [BankName], [BranchCode], [BranchName], [BranchAddress],
     [City], [State], [Pin], [Role], [IsActive], [FailedLoginCount],
     [CreatedAt], [UpdatedAt], [CreatedBy])
VALUES
    (1, N'bankadmin',
     N'AQAAAAIAAYagAAAAEIPBkSkagDEeRELmgDgCxJZinAKg/KQmO6hPeDdLe8u4haJi+IM8JF0dUZEbZgkqkA==',
     N'IDBI Bank Administrator', N'bankadmin@idbi.co.in', N'9000000001',
     N'Credit Manager', N'IBKL0000472', N'IDBI Bank', N'000472', N'Sitabuldi, Nagpur',
     N'IDBI Bank Ltd, Sitabuldi Main Road, Nagpur', N'Nagpur', N'Maharashtra', N'440012',
     N'BankAdmin', 1, 0,
     '2026-08-24T00:00:00', '2026-08-24T00:00:00', N'seed');

SET IDENTITY_INSERT [FinRiskLensAI].[ADM_BankLogin] OFF;
GO
```

> **Default credentials — `bankadmin` / `Bank@123`.** The stored hash is a real
> ASP.NET Identity V3 PBKDF2 hash of `Bank@123`, generated and round-trip
> verified with `PasswordHasher<T>`. **Change this password before any
> non-local deployment.** To mint a replacement hash, call
> `IBankAdminService.HashPassword(newPassword)` and update `PasswordHash`.

---

## 2026-08-24 — `t_MsmeScoreSummary` (new table)

A flat, queryable index over the blob-persisted `result.json` — one row per
UAN, upserted by `BlobAnalysisService.AnalyzeAsync` every time a score is
computed.

**Why it exists:** the full `RiskAnalysisResult` lives only in Azure Blob
storage (one JSON per MSME folder). The bank portal needs scores for *all*
customers at once, to sort, filter and chart them; fanning out one blob read per
customer does not scale past a handful of MSMEs. This table is the index — the
blob remains the source of truth for full detail.

```sql
CREATE TABLE [FinRiskLensAI].[t_MsmeScoreSummary]
(
    [MsmeScoreSummaryID]        INT             IDENTITY(1,1) NOT NULL,

    [Uan]                       NVARCHAR(25)    NOT NULL,   -- Udyam number = blob folder key

    -- Score
    [OverallScore]              FLOAT           NOT NULL,   -- 0-1000
    [ScoreBand]                 NVARCHAR(20)    NOT NULL,   -- Excellent/Good/Fair/AtRisk/HighRisk
    [HeuristicScore]            FLOAT           NOT NULL,
    [MlCalibratedScore]         FLOAT           NOT NULL,
    [CashflowTrendSlope]        FLOAT           NOT NULL,

    -- Lending headline figures
    [WorkingCapitalLimit]       DECIMAL(18,2)   NOT NULL,
    [TermLoanCapacity]          DECIMAL(18,2)   NOT NULL,
    [TotalIndicativeEligibility] DECIMAL(18,2)  NOT NULL,
    [AnnualTurnover]            DECIMAL(18,2)   NOT NULL,
    [MonthlySurplus]            DECIMAL(18,2)   NOT NULL,

    -- Top recommended product
    [TopProductName]            NVARCHAR(150)   NULL,
    [TopProductScheme]          NVARCHAR(50)    NULL,

    -- Flags / provenance
    [IsAnomalous]               BIT             NOT NULL,
    [DimensionsExcludedCount]   INT             NOT NULL,
    [ModelVersion]              NVARCHAR(50)    NULL,
    [ComputedAt]                DATETIME2(7)    NOT NULL,

    -- Audit (AuditableEntity)
    [CreatedAt]                 DATETIME2(7)    NOT NULL,
    [UpdatedAt]                 DATETIME2(7)    NOT NULL,
    [CreatedBy]                 NVARCHAR(100)   NULL,
    [UpdatedBy]                 NVARCHAR(100)   NULL,

    CONSTRAINT [PK_t_MsmeScoreSummary] PRIMARY KEY CLUSTERED ([MsmeScoreSummaryID] ASC)
);
GO

CREATE UNIQUE INDEX [IX_t_MsmeScoreSummary_Uan]          ON [FinRiskLensAI].[t_MsmeScoreSummary] ([Uan]);
CREATE        INDEX [IX_t_MsmeScoreSummary_ScoreBand]    ON [FinRiskLensAI].[t_MsmeScoreSummary] ([ScoreBand]);
CREATE        INDEX [IX_t_MsmeScoreSummary_OverallScore] ON [FinRiskLensAI].[t_MsmeScoreSummary] ([OverallScore]);
GO
```

### Backfill note

MSMEs scored before this table existed have a `result.json` in blob storage but
no row here, so the bank portal lists them as *Pending*. Re-running the analysis
writes both the blob and this table:

```
POST /api/msme-data/{uan}/analyze?force=true
```

After that no manual step is needed: `AnalyzeAsync` upserts here on every run.
A failed upsert is logged but never fails the analysis — the blob stays the
source of truth.

> A bulk "Sync scores" button existed briefly on the bank dashboard and was
> removed on request. To backfill several MSMEs, call the analyse endpoint above
> once per UAN.

---

## 2026-09-08 — `t_MsmeScoreHistory` (new table)

Migration `20260908164916_Add_MsmeScoreHistory`.

`t_MsmeScoreSummary` is **upserted** — one row per UAN, so every re-analysis
destroys the previous value. This table is **append-only**: one row per analysis
run, never updated, never deleted. It is what the **Score History** button on the
Financial Health Card and on Customer 360 draws, and it is the substrate an early
warning system compares against ("dropped a band since last month").

The six dimension scores are **columns, not JSON**, so an EWS rule can be a `WHERE`
clause rather than a deserialise-everything scan. `Source` tags where the row came
from — `analysis` for a real run, `seed` for the demo series, so fabricated points
stay identifiable forever.

```sql
CREATE TABLE [FinRiskLensAI].[t_MsmeScoreHistory]
(
    [MsmeScoreHistoryID]         INT             IDENTITY(1,1) NOT NULL,

    [Uan]                        NVARCHAR(25)    NOT NULL,

    -- Headline
    [OverallScore]               FLOAT           NOT NULL,
    [ScoreBand]                  NVARCHAR(20)    NOT NULL,
    [HeuristicScore]             FLOAT           NOT NULL,   -- rules engine, pre-calibration
    [MlCalibratedScore]          FLOAT           NOT NULL,   -- LightGBM output
    [CashflowTrendSlope]         FLOAT           NOT NULL,   -- SSA slope

    -- The six dimensions, as columns so an EWS rule can query them
    [RevenueVitality]            FLOAT           NOT NULL,
    [CashFlowHealth]             FLOAT           NOT NULL,
    [TransactionTrust]           FLOAT           NOT NULL,
    [ComplianceQuotient]         FLOAT           NOT NULL,
    [BusinessStability]          FLOAT           NOT NULL,
    [DebtServiceability]         FLOAT           NOT NULL,

    -- Lending figures at that point in time
    [AnnualTurnover]             DECIMAL(18,2)   NOT NULL,
    [MonthlySurplus]             DECIMAL(18,2)   NOT NULL,
    [TotalIndicativeEligibility] DECIMAL(18,2)   NOT NULL,

    -- Run context
    [IsAnomalous]                BIT             NOT NULL,
    [DimensionsExcludedCount]    INT             NOT NULL,
    [DataFilesUsed]              INT             NOT NULL,
    [ModelVersion]               NVARCHAR(50)    NULL,
    [ComputedAt]                 DATETIME2       NOT NULL,
    [Source]                     NVARCHAR(30)    NULL,       -- 'analysis' | 'seed'

    [CreatedAt]                  DATETIME2       NOT NULL,
    [UpdatedAt]                  DATETIME2       NOT NULL,
    [CreatedBy]                  NVARCHAR(100)   NULL,
    [UpdatedBy]                  NVARCHAR(100)   NULL,

    CONSTRAINT [PK_t_MsmeScoreHistory] PRIMARY KEY CLUSTERED ([MsmeScoreHistoryID] ASC)
);
GO

-- The only read pattern is "this UAN, newest first". Deliberately NOT unique:
-- the table is append-only and two runs in the same second are legitimate.
CREATE INDEX [IX_MsmeScoreHistory_Uan_ComputedAt]
    ON [FinRiskLensAI].[t_MsmeScoreHistory] ([Uan], [ComputedAt]);
GO
```

`AnalyzeAsync` appends here on every run, alongside the `t_MsmeScoreSummary`
upsert. A failed append is logged but never fails the analysis.

Demo series: `Doc/sql/demo_score_history_seed.sql` (eight monthly points per demo
account; deletes only `Source = 'seed'` rows, so real history survives).

---

## 2026-09-15 — `t_LoanCasePush` (new table)

Migrations `20260915172801_Add_LoanCasePush` and
`20260915173826_Add_LoanCasePush_EnterpriseName`.

One row per attempt to raise a scored MSME as a loan case in **LOS**, **ULI** or
**ONDC** from the bank portal. **Append-only** — never updated, never
de-duplicated: a case can legitimately go to LOS *and* ULI, and a re-push after a
re-score is a real event a bank must be able to audit. "Already sent" is simply
the latest row per `(Uan, Channel)`.

Score, band, eligibility and enterprise name are **frozen on the row**: six months
on you need to know what they were *when the case was raised*, not what they are
now. `Payload` holds the exact JSON that went over the wire (or would have, when
simulated), which is what the receipt page renders.

`Channel` and `Status` are stored as **their names, not ints** — a DBA reading the
table should see `ULI` / `Simulated`, and adding a channel must never renumber
history.

```sql
CREATE TABLE [FinRiskLensAI].[t_LoanCasePush]
(
    [LoanCasePushID]     INT             IDENTITY(1,1) NOT NULL,

    [Uan]                NVARCHAR(25)    NOT NULL,
    [EnterpriseName]     NVARCHAR(250)   NULL,       -- frozen at push time

    [Channel]            NVARCHAR(10)    NOT NULL,   -- 'LOS' | 'ULI' | 'ONDC'
    [Status]             NVARCHAR(12)    NOT NULL,   -- 'Sent' | 'Simulated' | 'Failed'

    -- Wire detail
    [CaseReference]      NVARCHAR(80)    NULL,       -- downstream id, or generated when simulated
    [Endpoint]           NVARCHAR(500)   NULL,       -- NULL when simulated
    [HttpStatus]         INT             NULL,
    [Payload]            NVARCHAR(MAX)   NOT NULL,
    [ResponseBody]       NVARCHAR(MAX)   NULL,
    [ErrorMessage]       NVARCHAR(1000)  NULL,

    -- Frozen at push time
    [ScoreAtPush]        FLOAT           NOT NULL,
    [BandAtPush]         NVARCHAR(20)    NOT NULL,
    [EligibilityAtPush]  DECIMAL(18,2)   NOT NULL,
    [ModelVersion]       NVARCHAR(50)    NULL,
    [ScoreComputedAt]    DATETIME2       NOT NULL,   -- how stale the score was

    -- Who
    [PushedByLoginId]    INT             NOT NULL,
    [PushedByUserId]     NVARCHAR(100)   NOT NULL,
    [PushedByName]       NVARCHAR(150)   NULL,
    [PushedFromIp]       NVARCHAR(64)    NULL,
    [PushedAt]           DATETIME2       NOT NULL,

    [CreatedAt]          DATETIME2       NOT NULL,
    [UpdatedAt]          DATETIME2       NOT NULL,
    [CreatedBy]          NVARCHAR(100)   NULL,
    [UpdatedBy]          NVARCHAR(100)   NULL,

    CONSTRAINT [PK_t_LoanCasePush] PRIMARY KEY CLUSTERED ([LoanCasePushID] ASC)
);
GO

-- "Latest per (Uan, Channel)" is the only read pattern — one index serves both the
-- Customer 360 chips and the Customers-list badge. Not unique: append-only.
CREATE INDEX [IX_LoanCasePush_Uan_Channel_PushedAt]
    ON [FinRiskLensAI].[t_LoanCasePush] ([Uan], [Channel], [PushedAt]);
GO
```

`EnterpriseName` was added an hour after the table (second migration) because the
receipt header reads better as the enterprise name than as a UAN:

```sql
ALTER TABLE [FinRiskLensAI].[t_LoanCasePush] ADD [EnterpriseName] NVARCHAR(250) NULL;
GO
```

**No endpoint = no HTTP call.** `LoanCase:Endpoints:{LOS|ULI|ONDC}` in
`appsettings` is blank by default, so a push is written here with
`Status = 'Simulated'` and a generated reference (`IBKL-LOS-yyyyMMdd-xxxxxx`),
carrying the payload that *would* have been sent. Setting a URL turns the same
code path into a real POST — `Status` becomes `Sent` or `Failed`.

---

## 2026-09-30 — PostgreSQL baseline

Migration `20260930174140_PostgresBaseline` (in `Migrations/Postgres/`). A fresh baseline,
not a conversion: the SQL Server migrations were retired (kept in git history) and the
schema was regenerated for Npgsql. Applied to Neon via the **direct** endpoint.

**Carried over (12):** `ADM_BankLogin`, `ADM_Login`, `m_StaticResponces`,
`t_AAConsentRequest`, `t_AccAggreToken`, `t_GSTR2And3BResponce`, `t_LoanCasePush`,
`t_MsmeEnquiry`, `t_MsmeLocations`, `t_MsmeNicCodes`, `t_UserOtp`, `t_UserRegistration`.

**Dropped:** `t_MsmeScoreSummary` and `t_MsmeScoreHistory` — the scoring engine is gone.
`SessionCache` is no longer used; sessions are in-memory (`Program.cs` explains the ceiling).

**Altered:** `t_LoanCasePush` loses `ScoreAtPush`, `BandAtPush`, `EligibilityAtPush`,
`ModelVersion` and `ScoreComputedAt`. The `payload` column already holds the exact bytes
sent, which is the frozen snapshot.

**Created — the RAG knowledge base (R1):** `knowledge_docs`, `knowledge_chunks`,
`chunk_embeddings`, `rag_query_log`. Design in `11_RAG_ARCHITECTURE.md` §14. What the
baseline actually creates for retrieval:

```sql
CREATE EXTENSION IF NOT EXISTS vector;

-- sparse arm: generated, so it is correct on every insert with no ingest step
search_vector tsvector GENERATED ALWAYS AS
    (to_tsvector('english', coalesce(breadcrumb, '') || ' ' || chunk_text)) STORED
CREATE INDEX ix_knowledge_chunks_search_vector ON knowledge_chunks USING GIN (search_vector);

-- dense arm
embedding vector(768) NOT NULL
CREATE INDEX ix_chunk_embeddings_embedding ON chunk_embeddings
    USING hnsw (embedding vector_cosine_ops) WITH (m = 16, ef_construction = 64);

-- one vector per chunk per model, so a re-embed writes alongside the old vectors
CREATE UNIQUE INDEX ... ON chunk_embeddings (knowledge_chunk_id, model_id);
```

**Verified on Neon after applying** (PostgreSQL 18.6, pgvector 0.8.6): all 16 tables
present, both indexes present, `search_vector` generated, `embedding` typed
`vector(768)`, no score tables. A rolled-back smoke insert proved both retrieval arms: a
full-text query for `FOIR` matched, and a vector's cosine distance to itself was 0.

**Seeded by the migration:** the bank portal login `bankadmin` (`HasData` in
`AdmBankLoginConfiguration`), so a fresh database has a working sign-in with no manual step.

### Reproducing

```bash
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
```

On Neon, run schema changes against the direct endpoint — drop `-pooler` from the host —
by overriding for the one command rather than editing the settings file:

```bash
Database__ConnectionStrings__PostgreSQL="<direct connection string>" dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
```

---

## 2026-10-02 — Case schema

Migration `20261002102310_CaseSchema`. The case side of FraudLens
(`10_FRAUD_LENS_BUILD.md` §4): an application, the documents uploaded for it, and the
tables the extractor fills and the Phase 3 deterministic checks read.

```sql
CREATE TABLE applications (
    loan_application_id integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    applicant_ref     varchar(50)  NOT NULL,          -- lender's application number, never a name
    product_type      varchar(20)  NOT NULL,          -- SmeLoan | PersonalLoan | Card
    uan               varchar(30),                    -- Udyam number: key for the verification sources
    declared_income   numeric(14,2),                  -- monthly
    declared_employer varchar(200),
    declared_emis     numeric(14,2),                  -- monthly; baseline for UNDECLARED_EMI
    submitted_at      timestamp    NOT NULL,
    status            varchar(15)  NOT NULL,          -- New | Processing | Review | Clear | Hold
    created_at, updated_at, created_by, updated_by    -- AuditableEntity
);
CREATE INDEX ix_applications_submitted_at ON applications (submitted_at);
CREATE INDEX ix_applications_uan ON applications (uan);

CREATE TABLE documents (
    case_document_id    integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    loan_application_id integer NOT NULL REFERENCES applications ON DELETE RESTRICT,
    file_name      varchar(255) NOT NULL,
    content_type   varchar(100) NOT NULL,             -- sniffed from the bytes, not the upload
    size_bytes     bigint       NOT NULL,
    storage_path   varchar(400) NOT NULL,             -- cases/{sha256}{ext}, content-addressed
    sha256         bytea        NOT NULL,
    doc_type       varchar(20)  NOT NULL,             -- Unclassified until the extractor runs
    page_count     integer,
    pdf_producer   varchar(200), pdf_creator varchar(200),
    pdf_created_at timestamp,    pdf_modified_at timestamp,
    font_set       varchar(1000),
    claimed_issuer varchar(200), layout_text text,    -- extractor output
    extract_status varchar(10)  NOT NULL,             -- Pending | Done | Failed
    extract_error  text,
    created_at, updated_at, created_by, updated_by
);
CREATE INDEX ix_documents_loan_application_id ON documents (loan_application_id);
CREATE INDEX ix_documents_sha256 ON documents (sha256);   -- same file in two cases

CREATE TABLE statement_rows (
    statement_row_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    case_document_id integer NOT NULL REFERENCES documents ON DELETE CASCADE,
    page_no integer NOT NULL, row_no integer NOT NULL,
    txn_date date NOT NULL, description varchar(400) NOT NULL,
    debit numeric(14,2), credit numeric(14,2), balance numeric(14,2),
    counterparty_name varchar(200), counterparty_acct varchar(50), counterparty_class varchar(20),
    box varchar(40)                                   -- "ymin,xmin,ymax,xmax", 0–1000
);
CREATE UNIQUE INDEX ix_statement_rows_case_document_id_row_no ON statement_rows (case_document_id, row_no);

CREATE TABLE page_summaries (
    case_document_id integer NOT NULL REFERENCES documents ON DELETE CASCADE,
    page_no integer NOT NULL,
    opening_balance numeric(14,2), closing_balance numeric(14,2),
    stated_total_debit numeric(14,2), stated_total_credit numeric(14,2),
    PRIMARY KEY (case_document_id, page_no)
);

CREATE TABLE doc_fields (
    doc_field_id bigint GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    case_document_id integer NOT NULL REFERENCES documents ON DELETE CASCADE,
    name varchar(50) NOT NULL, value varchar(400) NOT NULL, num_value numeric(14,2),
    page_no integer NOT NULL, box varchar(40)
);
CREATE INDEX ix_doc_fields_case_document_id_name ON doc_fields (case_document_id, name);

-- RagQueryLog now names the entity it points at
ALTER TABLE rag_query_log RENAME COLUMN application_id TO loan_application_id;
ALTER INDEX ix_rag_query_log_application_id RENAME TO ix_rag_query_log_loan_application_id;
```

Not yet: `findings`, `evidence`, `rings`, `ai_drop_log` (Phase 3–5), and the layout
embedding on `documents` (Phase 5, network lens). Each lands with the code that writes it.

---

## Applying changes

Preferred — EF migration from the repository root:

```bash
dotnet ef migrations add <MigrationName> --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
```

```bash
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
```

If EF tooling is unavailable, run the `CREATE TABLE` scripts above by hand and
insert the matching row into `[cre].[__EFMigrationsHistory]` so EF does not try
to recreate the tables later.
