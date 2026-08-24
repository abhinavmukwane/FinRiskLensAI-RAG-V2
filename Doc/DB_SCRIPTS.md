# Database Scripts

Running log of every table created, altered or extended, with the SQL to apply
it by hand when a migration cannot be run.

**Convention:** each entity keeps an int identity primary key named
`<EntityName>ID`. Audit columns (`CreatedAt`, `UpdatedAt`, `CreatedBy`,
`UpdatedBy`) come from `AuditableEntity` and are stamped automatically in
`ApplicationDbContext.SaveChangesAsync`.

**Schema:** application tables live in the `[FinRiskLensAI]` schema — the
default schema of the `FinRiskLensAI` SQL login — because the entity
configurations call `ToTable("Name")` without an explicit schema. Only the EF
migrations history table is pinned elsewhere, at
`[cre].[__EFMigrationsHistory]` (set in `DataServiceCollectionExtensions`).
Adjust the prefix if your environment differs.

Current UAT target: `103.21.58.192` / `FinRiskLensAI_UAT`
(`appsettings.Development.json`).

**Add a new entry to this file whenever a table is created, a column is added,
or a column is altered — before running the migration.**

---

## Index

| Date | Change | Table |
|---|---|---|
| 2026-08-24 | Create — bank portal login | `ADM_BankLogin` |
| 2026-08-24 | Create — SQL index over blob `result.json` | `t_MsmeScoreSummary` |

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
no row here, so the bank portal shows them as *Pending*. Two ways to populate:

**Bulk (preferred)** — the **Sync scores** button on the bank portal dashboard,
or the endpoint behind it. It walks every onboarded UAN, reads the stored
`result.json` and upserts a row. Safe to re-run; it never recomputes a score.

```
POST /BankAdmin/ResyncScores        (bank session + antiforgery token required)
```

**Single MSME** — re-running the analysis writes both the blob and this table:

```
POST /api/msme-data/{uan}/analyze?force=true
```

After the initial backfill no manual step is needed: `AnalyzeAsync` upserts here
on every run. A failed upsert is logged but never fails the analysis — the blob
stays the source of truth.

---

## Applying changes

Preferred — EF migration from the repository root:

```bash
dotnet ef migrations add <MigrationName> --project FinRiskLensAI.Data --startup-project FinRiskLensAI
```

```bash
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI
```

If EF tooling is unavailable, run the `CREATE TABLE` scripts above by hand and
insert the matching row into `[cre].[__EFMigrationsHistory]` so EF does not try
to recreate the tables later.
