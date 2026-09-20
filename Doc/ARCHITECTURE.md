# FinRiskLensAI — Architecture

*(updated 2026-07-04 — reflects the implemented state: ML engine, Azure Blob data
flow, int-identity entity convention, remote SQL Server)*

## 1. Overview

**FinRiskLensAI** is an ASP.NET Core 8 (MVC) application organized as a layered
solution: an MSME Financial Health Score platform (IDBI Innovate 2026) that
aggregates alternate data (Udyam, GST, ITR, AA bank statements; EPFO later),
computes a six-dimension 0–1000 score with **ML.NET**, derives the bank-decision
ratios and indicative loan eligibility, and renders a Financial Health Card
dashboard. Composition via **Autofac**, persistence via **EF Core 8**, raw
payload storage via **Azure Blob Storage**, logging via **Serilog**.

| Property            | Value                                                  |
| ------------------- | ------------------------------------------------------ |
| Target framework    | `net8.0`                                               |
| Web stack           | ASP.NET Core MVC (Controllers + Views + API endpoints) |
| DI container        | Autofac (via `AutofacServiceProviderFactory`)          |
| ORM                 | EF Core 8 — SQL Server (remote), Npgsql branch unused  |
| ML                  | ML.NET 3 (LightGBM, SSA time-series, RandomizedPca)    |
| Payload storage     | Azure Blob Storage (container `msme-data`)             |
| Logging             | Serilog (Console + rolling File sinks)                 |
| Auth                | JWT config present; bearer handler **not yet wired**   |

---

## 2. Solution Structure

```
F:\FinRiskLensAI_Git\
├── FinRiskLensAI.sln
│
├── FinRiskLensAI\                 → Web / Presentation layer (startup project)
│   ├── Controllers\               HomeController · OnboardingController
│   │                              DashboardController   (Financial Health Card UI)
│   │                              MsmeDataController    (blob upload/status/analyze API)
│   │                              ScoringController     (direct analyze API — dev/test)
│   ├── Models\                    ErrorViewModel · FinancialHealthCardViewModel
│   ├── Views\                     Home · Onboarding · Dashboard\FinancialHealthCard.cshtml
│   ├── wwwroot\                   static assets (bootstrap, css, js)
│   ├── Program.cs                 host bootstrap, DI, middleware pipeline
│   └── appsettings.json           DB, AzureBlob, JWT, Serilog config
│
├── FinRiskLensAI.Core\            → Domain layer (contracts + models, no infra deps)
│   ├── Interfaces\                IRepository<T> · IRiskScoringService (+ RiskAnalysisRequest)
│   │                              IBlobAnalysisService (+ MsmeDataStatusReport) · IMsmeDataStore
│   ├── Models\Common\             AuditableEntity (audit fields only — no base Id)
│   ├── Models\Admin\              AdmLogin
│   ├── Models\Onboarding\         MsmeEnquiry · MsmeLocation · MsmeNicCode
│   ├── Models\Scoring\            RiskAnalysisResult (+ ScoreBandType, ImpactDirection)
│   │                              MsmeFeatureSet · LendingAssessment (+ RatioStatus)
│   ├── Models\Storage\            MsmeDataManifest (+ MsmeDataStatus) · MsmeDataFiles
│   └── DI\CoreModule.cs
│
├── FinRiskLensAI.ML\              → AI/ML engine (implementations only; contracts in Core)
│   ├── Features\                  Udyam/Gst/Itr/Aa feature extractors · TrendMath
│   ├── MachineLearning\           ScoreCalibrationModel (LightGBM + PFI)
│   │                              CashflowTrendAnalyzer (SSA) · IncomeAnomalyDetector (PCA)
│   │                              ScoreFeatureVector · SyntheticProfileGenerator
│   ├── Services\                  RiskScoringService · BlobAnalysisService · LendingCalculator
│   ├── Storage\                   AzureBlobDataStore
│   └── DI\MLModule.cs
│
├── FinRiskLensAI.Services\        → Application layer (business services; DI scaffold)
│   └── DI\ServicesModule.cs
│
└── FinRiskLensAI.Data\            → Infrastructure / Persistence layer
    ├── DbContextEDMX\             ApplicationDbContext · ApplicationDbContextFactory
    ├── Configurations\            IEntityTypeConfiguration<T> per entity
    ├── Migrations\SqlServer\      single InitialCreate baseline (int identity PKs)
    ├── Repositories\RepositoryBase.cs
    └── DI\DataModule.cs
```

---

## 3. Layered Architecture

Clean/Onion layering — dependencies point inward to `Core`, which holds all
**contracts** (interfaces) and **models** (including the scoring/lending/storage
shapes). The ML project is an infrastructure-style implementation layer, like
`Data`: it implements Core interfaces and is only reachable elsewhere through
them.

| Project        | References           | Role                                        |
| -------------- | -------------------- | ------------------------------------------- |
| **Web**        | `Core`, `Services`, `ML` (for DI module + implementations resolution) | controllers, views, composition root |
| **Services**   | `Core`, `Data`       | business/use-case services                  |
| **Data**       | `Core`               | EF Core persistence                         |
| **ML**         | `Core`               | scoring engine, blob store, ML.NET models   |
| **Core**       | *(none)*             | entities, contracts, result models, enums   |

Rule of thumb: **anything two layers need to agree on lives in `Core`**
(e.g. `RiskAnalysisResult`, `IMsmeDataStore`); anything that needs a package
(EF, ML.NET, Azure SDK) lives in the implementing layer.

---

## 4. Dependency Injection (Autofac)

```csharp
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container =>
{
    container.RegisterModule(new CoreModule());
    container.RegisterModule(new DataModule());
    container.RegisterModule(new ServicesModule());
    container.RegisterModule(new MLModule());
});
```

| Module           | Registration strategy                                                                                     |
| ---------------- | --------------------------------------------------------------------------------------------------------- |
| `CoreModule`     | placeholder (empty)                                                                                        |
| `DataModule`     | assembly scan `*Repository` → interfaces; generic `RepositoryBase<>` → `IRepository<>`                     |
| `ServicesModule` | assembly scan `*Service` → interfaces                                                                      |
| `MLModule`       | extractors + ML models as **singletons** (models train lazily, once per process); scan `*Service`; `AzureBlobDataStore` → `IMsmeDataStore` |

> Convention: `*Repository` / `*Service` naming + an interface = automatic
> registration. ML model singletons matter — LightGBM/PCA training (~1–2 s)
> happens once on first use.

---

## 5. Domain Model Conventions

**Changed 2026-07-03** — the earlier `BaseEntity` (Guid `Id` + `IsDeleted`) was
removed. Current rules:

```csharp
public abstract class AuditableEntity        // audit fields only
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
```

- Every entity declares its own **int identity primary key named
  `<EntityName>ID`** — `AdmLoginID`, `MsmeEnquiryID`, `MsmeLocationID`,
  `MsmeNicCodeID` (`UseIdentityColumn()` in its configuration).
- No Guid keys, no shared `Id`, no soft-delete flag.
- Audit timestamps are stamped automatically in
  `ApplicationDbContext.SaveChangesAsync`.

Implemented tables: `ADM_Login` (seeded admin), `t_MsmeEnquiry` (Udyam response
cache, unique per UAN, raw JSON in `Payload`), `t_MsmeLocations`,
`t_MsmeNicCodes`. The core scoring entities from `01_DOMAIN_MODEL.md`
(`Msme`, `ScoreComputation`, …) are still pending.

---

## 6. Persistence

- `ApplicationDbContext` — decimal `precision(18,4)` convention,
  `ApplyConfigurationsFromAssembly`, automatic audit stamping.
- `RepositoryBase<T> : IRepository<T> where T : class` — async CRUD;
  `GetByIdAsync(int id)` uses `FindAsync`, so it works with any single int key
  regardless of the property name. No internal `SaveChanges` (unit-of-work by
  the caller).
- Migrations: one clean `InitialCreate` baseline under
  `Data/Migrations/SqlServer/`; history table `cre.__EFMigrationsHistory`.
  Commands are documented in `DB_SCRIPTS.md`. Runtime connection comes from
  `appsettings.json`, design-time from `ApplicationDbContextFactory` — keep in
  sync.

---

## 7. The ML Engine & Data Flow (summary — details in `08_ML_ENGINE.md`)

```
data pulls (Udyam/GST/ITR/AA APIs)                    credit officer / MSME
        │  raw JSON per response                                ▲
        ▼                                                       │
Azure Blob  msme-data/{UAN}/…  ──►  BlobAnalysisService  ──►  result.json
   (manifest gates completeness)          │                     │
                                          ▼                     ▼
                              RiskScoringService        Dashboard/FinancialHealthCard
                       extractors → 6 dimensions →      (gauge, radar, bars, ratios,
                       LightGBM blend → anomaly →        lending assessment, Chart.js)
                       explanations → recommendations →
                       LendingCalculator (ratios + eligibility)
```

- **Two API entry paths:** `POST /api/scoring/analyze` (payloads in body —
  dev/test) and `api/msme-data/{uan}` (blob flow — production path, handles any
  payload size; one file per request, one file in memory at a time).
- **Output** (`RiskAnalysisResult` in Core): overall score + band, six
  `DimensionScore`s, PFI-template explanations, product recommendations,
  cross-source anomaly check, the **`LendingAssessment`** (12 appraisal ratios
  incl. DSCR/FOIR/banking penetration/GSTR-1-vs-3B consistency/ITR financial
  ratios, plus indicative working-capital and term-loan eligibility scaled by
  score band), and four **underwriting deep-dives**: `BankStatementAnalysis`
  (AMB, channel split, payment classification, return counts),
  `GstDeepDive` (tax discipline, ITC, customer/vendor concentration),
  `FinancialRatios` (ITR P&L/balance-sheet), `IndustryRiskInfo`
  (NIC sector risk weight).

---

## 8. Configuration

- **`Database`** — `DbType` switch; SQL Server connection to the remote shared
  instance (login `FinRiskLensAI`; tables land in that login's default schema).
  PostgreSQL string retained but unused.
- **`AzureBlob`** — connection string + container (`msme-data`) for the per-UAN
  payload folders.
- **`Jwt`** — issuer `FinRiskLensAI`, audience `MsmeClients`; **handler not yet
  registered** (`AddAuthentication().AddJwtBearer` absent) — required before the
  external APIs in `04_API_CONTRACTS.md` ship.
- **`Serilog`** — console + daily rolling file, per-namespace overrides.

> ⚠️ **Secrets:** DB credentials and the storage account key are checked into
> `appsettings.json` as a hackathon trade-off. Rotate and move to a secret store
> before anything public.

---

## 9. Key NuGet Dependencies

| Package                                              | Project | Purpose                                  |
| ---------------------------------------------------- | ------- | ---------------------------------------- |
| `Autofac` (+ DI extensions)                          | all     | IoC container                            |
| `Microsoft.EntityFrameworkCore` (+ SqlServer, Design)| Data, Web | ORM + tooling                          |
| `Microsoft.ML` / `.LightGbm` / `.TimeSeries`         | ML      | scoring model, SSA trend, PCA anomaly    |
| `Azure.Storage.Blobs`                                | ML      | per-UAN payload folders                  |
| `Microsoft.AspNetCore.Authentication.JwtBearer`      | Web     | JWT bearer (pending wiring)              |
| `Serilog` (+ sinks/enrichers)                        | all     | structured logging                       |
| `Newtonsoft.Json`                                    | ML, Data| tolerant payload parsing, result persist |

---

## 10. Extending the Application

1. **Domain** — entity in `Core/Models/...` deriving from `AuditableEntity`,
   with its own `<EntityName>ID` int key; contracts in `Core/Interfaces`;
   shared result/DTO shapes in `Core/Models/...`.
2. **Persistence** — `IEntityTypeConfiguration<T>` (+ `UseIdentityColumn()` on
   the key) in `Data/Configurations`; `DbSet` on the context; migration via the
   commands in `DB_SCRIPTS.md`.
3. **Application/ML** — `IFooService` in Core, `FooService` in `Services` (or
   `ML` if it needs ML/storage packages); auto-registers by naming convention.
4. **Presentation** — controller depends on the Core interface; Razor views;
   API endpoints follow the existing `api/...` attribute-routing style.

---

## 11. Known Gaps / Notes

- **JWT bearer handler still unregistered** — the one remaining item from
  `07_SETUP_GAPS.md`.
- `Services` project is still a scaffold — onboarding/consent services from
  `02_CUSTOMER_FLOW.md` Steps 1–6 are unbuilt.
- Scoring output is persisted as `result.json` in the blob folder, not yet
  mapped to `ScoreComputation`/`ScoreExplanation` entities.
- `m_StaticResponces` exists in the DB but its entity code was never pushed —
  re-add under the new convention (`StaticResponcesID`).
- The folder name `DbContextEDMX` is legacy-flavored; the context is code-first
  EF Core, not an EDMX model.
