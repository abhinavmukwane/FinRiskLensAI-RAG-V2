# Session Handoff — FinRiskLensAI (as of 2026-07-08)

Context file for Claude Code. This summarizes the project state, decisions made,
and working conventions from the development session on the office PC, so work can
continue seamlessly. Read `Doc/00_README.md` → `Doc/08_ML_ENGINE.md` for the full
product/business context; this file covers what's *built and decided*.

## What this project is

MSME Financial Health Score platform for **IDBI Innovate 2026** (deadline **July 31,
2026**). Aggregates alternate data (Udyam, GST, ITR, AA bank statements; EPFO later)
for New-to-Credit MSMEs, computes a 6-dimension 0–1000 score with ML.NET, and serves
it to IDBI's LOS and the ULI/OCEN ecosystem. ASP.NET Core 8 MVC, EF Core 8, Autofac,
Serilog. Layering: Core → Data → Services → Web, plus a new ML project.

## Current state — everything below is BUILT, TESTED, and PUSHED to main

### 1. Database (SQL Server, remote)
- Connection: remote SQL Server at `103.21.58.192` (moved from 4.247.173.231 on
  2026-07-03), DB `FinRiskLensAI`, SQL auth (login `FinRiskLensAI`) — real string in
  `appsettings.Development.json` (`Database:ConnectionStrings:SqlServer`); `appsettings.json`
  has a placeholder now (see §7b). `ApplicationDbContextFactory` (design-time) still hardcodes
  it — keep it in sync / read from config. `TrustServerCertificate=True` required.
  Tables land in the login's default schema `FinRiskLensAI`, not `dbo`. PostgreSQL
  branch kept in code but unused.
- **PK convention (2026-07-03): int identity, named `<EntityName>ID`**
  (`AdmLoginID`, `MsmeEnquiryID`, …) — NOT Guid, NOT a shared base `Id`.
  `BaseEntity` was deleted; `AuditableEntity` has audit fields only; no
  `IsDeleted`/soft-delete. `IRepository<T>.GetByIdAsync` takes `int`. Migrations
  were re-baselined to a single `InitialCreate` under this convention.
- ~~⚠ `m_StaticResponces` entity/config missing from git~~ — resolved 2026-07-05
  (entity + config now in code; served via `UdyamController` / `IStaticResponseService`).
- DbContext registered via `DataServiceCollectionExtensions.AddAppDbContext()` (in
  Data project), called from `Program.cs`. Generic `IRepository<T>` →
  `RepositoryBase<T>` registered in `DataModule`.
- EF migrations live in `FinRiskLensAI.Data/Migrations/SqlServer/` (design-time
  factory fixed — no separate migrations assemblies). History table: `cre.__EFMigrationsHistory`.
- **Tables applied to the remote DB:**
  - `ADM_Login` — admin login (Username/Email unique, PasswordHash, Role, IsActive,
    LastLoginAt). Seeded admin: username `admin`; the password was shared with
    Abhinav during the session (ask him — deliberately not written here). Hash is
    ASP.NET Identity V3 PBKDF2 format (verify with `PasswordHasher<T>`).
  - `t_MsmeEnquiry` — Udyam API response cache, one row per UAN (unique index).
    Columns from `main_details` (excl. `enterprise_type_list`) + full raw JSON in
    `Payload` (nvarchar(max)). Cache flow: hit table by UAN first, call API only on miss.
  - `t_MsmeLocations` / `t_MsmeNicCodes` — child tables (FK `MsmeEnquiryId`, cascade)
    for `location_of_plant_details` and `nic_code` arrays.
  - **Added 2026-07-05:**
    - `t_UserRegistration` (`UserRegistrationModel`) — customer account: Mobile, Email,
      UdyamNumber, GstinNumber, PanNumber, FK `MsmeEnquiryID`. Indexed on all lookup cols.
    - `t_UserOtp` (`UserOtpModel`) — login OTP per user (Email, encrypted `OTP`,
      MobileNumber, FKs). OTP stored **encrypted** (`IEncryption`); one row per email
      (upsert in `AddUpdateUserOtp`).
    - `t_AccAggreToken` (`AccAggreTokenModel`) — single-row Finvu login-token store
      (rid/ts/token/UpdatedOn), token valid 23h.
    - `t_AAConsentRequest` (`AAConsentReqModel`) — one row per AA consent request:
      uan, **custId** (persisted so the cross-site callback can re-derive AAID
      without a session), transactionId, header (rid/ts/channelId), encryptedRequest,
      requestDate, encryptedFiuId, consentHandle, url, CreatedOn.
    - `m_StaticResponces` — now has entity/config in code (moved to `UdyamController`,
      backed by `IStaticResponseService`); the earlier ⚠ gap is closed.
- Entities in `Core/Models/{Admin,Onboarding}`, configs in `Data/Configurations/`,
  all derive from `AuditableEntity`.
- **Migration commands** (run from the solution root, `F:\FinRiskLensAI_Git`; needs
  the `dotnet-ef` global tool — `dotnet tool install --global dotnet-ef`):

  ```powershell
  # add a new migration (SQL Server is the active provider)
  dotnet ef migrations add <MigrationName> --project FinRiskLensAI.Data --startup-project FinRiskLensAI --output-dir Migrations/SqlServer

  # apply pending migrations to the database in appsettings/factory
  dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI

  # check which migrations are applied vs pending
  dotnet ef migrations list --project FinRiskLensAI.Data --startup-project FinRiskLensAI

  # undo the last (unapplied) migration
  dotnet ef migrations remove --project FinRiskLensAI.Data --startup-project FinRiskLensAI
  ```

  `--output-dir Migrations/SqlServer` matters — keeps SQL Server migrations separate
  from the (unused) PostgreSQL folder. Design-time commands use the connection string
  in `ApplicationDbContextFactory` (Data project), runtime uses `appsettings.json` —
  both currently point at the same remote server; keep them in sync when it changes.
  Note: `database update` runs against the REMOTE shared DB — teammates share it.

### 2. ML.NET scoring engine — `FinRiskLensAI.ML` project (see Doc/08_ML_ENGINE.md)
- Feature extractors (Newtonsoft `JObject`, tolerant probing) for Udyam, GST, ITR
  (handles ITR-1 and ITR-3 shapes across up to 3 years), AA (dedupes txns by
  `txnId` — same txns appear under multiple FIPs in real responses).
- GST covers seven return types: taxpayer profile, monthly GSTR-3B (turnover),
  GSTR-1 summary (B2B share/counterparties), GSTR-1 B2B/e-invoices, GSTR-1 CDNR
  (credit-note revenue reversals), GSTR-1 HSN (product-mix diversity), GSTR-2A
  (purchase-to-sales trade cycle), and **GSTR-2B** (`gstr2b_get_all_details_MMyyyy.json`,
  added 2026-07-07: ITC claimed-vs-available over-claim check, ITC-unavailable
  share, supplier filing rate; payload is DOUBLE-nested `response.message.data.data`).
  **Real GST API responses are wrapped in a `response.message.data` envelope** —
  `GstFeatureExtractor.Unwrap()` handles wrapped and bare payloads. Appraisal
  table is now 14 ratios (added ITC-vs-2B ≤100% and supplier filing ≥90%).
- Six dimensions with doc weights (25/20/15/15/15/10), missing-source weight
  redistribution, NTC neutral default on Debt Serviceability (never zero).
  Missing sources feed neutral values into the ML vector too, not zeros.
- ML.NET: LightGBM calibration on a 15-feature vector (lazy-trained on 3000
  synthetic profiles; final score = 0.6 heuristic + 0.4 model), per-instance
  permutation importance → fixed template explanations, SSA cashflow trend,
  RandomizedPca 3-way income anomaly check (GST vs ITR vs bank credits —
  data-integrity signal, separate from score).
- Verified twice: demo payloads **748/1000 Good**; real 6-month GST data for
  UDYAM-MH-20-0067394 (UCN Fibrenet) **719/1000 Good** (₹2.99 cr/mo turnover,
  Compliance 150/150). Known gap: that folder's aa.json is demo data mismatched
  to the business; anomaly score 0.426 sat just under the 0.5 flag threshold —
  tune thresholds when real paired AA data exists.
- **Clean layering (2026-07-04):** all interfaces/models/enums moved to Core —
  `Core/Interfaces` (IRiskScoringService, IBlobAnalysisService, IMsmeDataStore),
  `Core/Models/Scoring` (RiskAnalysisResult, MsmeFeatureSet, LendingAssessment),
  `Core/Models/Storage` (MsmeDataManifest, MsmeDataFiles). ML project holds
  implementations only (ScoreFeatureVector stays in ML — it carries an ML.NET
  attribute).
- **Lending assessment (stage 8, `LendingCalculator`):** 7 bank ratios (DSCR,
  FOIR, banking penetration, gross margin, days-cash, inflow CV, credit-note
  ratio) with benchmarks + Strong/Adequate/Weak status, and indicative
  eligibility — WC via turnover/Nayak method (20% of turnover × band factor),
  term loan via EMI-headroom annuity (5y @ 11%). Formulas in
  `Doc/08_ML_ENGINE.md` Stage 8.
- **Underwriting deep-dives (stage 9, 2026-07-04):** four sections on every
  result — `BankStatementAnalysis` (AMB/peak from running balances, channel
  split, cash/salary/customer/supplier classification, cheque vs ECS-NACH
  returns, OD + min-balance breaches), `GstDeepDive` (GSTR-1 vs 3B consistency,
  cash-vs-ITC tax discipline, net ITC/mo, top-5 customer/vendor concentration
  with masked GSTINs), `FinancialRatios` (EBITDA/net margin from ITR-3 P&L,
  debtor days/asset turnover from ITR balance sheet, N/A when absent),
  `IndustryRiskInfo` (NIC-2 → static sector weight table, feeds 10% of Business
  Stability). Appraisal table now 12 ratios. Calculation formulas:
  `Doc/08_ML_ENGINE.md` Stages 8-9.
- **Financial Health Card dashboard built:** `Dashboard/FinancialHealthCard`
  (DashboardController + Chart.js) — UAN comes from the authenticated **session**,
  not a query param (see §5), so a user only sees their own card. Renders the score
  gauge with needle, dimension radar/bars, strengths/risks, anomaly box, Bank
  Lending Assessment (eligibility tiles, 12-ratio table, cashflow/EMI chart,
  methodology notes, industry chip), the Bank Statement Analysis + GST Deep-Dive
  panels, and a run-analysis button for folders without a result.

### 3. Two API entry paths
- `POST /api/scoring/analyze` (`ScoringController`) — payloads in the body. Kept
  deliberately as the stateless dev/test/what-if harness; NOT the production path.
- **Blob flow (primary)** — `api/msme-data/{uan}`: `PUT /manifest` (declare expected
  files), `PUT /files/{fileName}` (upload one raw payload at a time), `GET /status`,
  `POST /analyze?force=` (409 + missing list until manifest satisfied; force analyzes
  partial data), `GET /result`. Azure Blob container `msme-data`, folder per UAN,
  file conventions in `MsmeDataFiles` (`udyam.json`, `itr.json`, `aa.json`,
  `gst_taxpayer.json`, `epfo.json`, monthly GST files `gstr3b_MMyyyy.json`,
  `gstr1_summary_MMyyyy.json`, `gstr1_b2b_Invoice_MMyyyy.json`,
  `gstr1_cdnr_MMyyyy.json`, `gstr1_hsn_summary_MMyyyy.json`,
  `gstr2a_b2b_MMyyyy.json`, plus `_manifest.json` and `result.json`).
  Engine writes `result.json` back; manifest lifecycle Collecting→Processing→Completed/Failed.
- Azure Storage connection string in `appsettings.json` under `AzureBlob:*`
  (account `tflgspblobstorage`). Verified end-to-end against the real account;
  test folder `UDYAM-MH-20-0033382` may still exist there (contains real personal
  data — deletable).

### 4. UI
- Home page (`Views/Home/Index.cshtml`) rewritten to match the docs: six-dimension
  section with real weights, Udyam-first onboarding CTAs → `/Onboarding/CustOnboarding`.
- All icons are Bootstrap Icons (Material Symbols removed, including the font link).
- `.brandbar` header is sticky (both `Index.css` and `CustomerOnboarding.css`),
  `section[id] { scroll-margin-top: 90px }` keeps anchor targets clear of it.
- **Branding (2026-07-05):** `wwwroot/Logo.png` (full lockup) + `wwwroot/favicon.ico`
  wired into all three layouts (`_Layout`, `_CustOnboardingLayout`, `_DashboardLayout`) —
  favicon `<link>` in each `<head>`, logo replaces the old "FR" text mark / sidebar SVG.
- **Hero animation (`Index.cshtml`):** the bento cluster in the `col-lg-6` hero div —
  the two corner cards (`hero-float-id`/`hero-float-chart`) float via CSS keyframes; the
  center Financial Health Card is FIXED (no float, per request). Inside it a live score
  counts between realistic values with the band label + gauge needle following, tiles
  pulse, and the trend bars re-randomize. All scoped inline; `prefers-reduced-motion` honored.

### 5. Customer auth — email OTP + session (built 2026-07-05)
- **Email:** MailKit (`IEmailService`/`EmailService` in Services), STARTTLS. Config
  `Smtp:*` in appsettings — host **`sdin-pp-wb3.webhostbox.net`** (the shared-hosting
  server behind `finrisklensai.com`; using the domain name fails TLS cert validation),
  port 587, `EnableSsl:true`, `AllowCertificateNameMismatch:true` (accepts the host's
  `*.webhostbox.net` cert — the ONLY validation error tolerated). Branded HTML OTP
  template in `Services/Templates/LoginOtpEmailTemplate.cs`. Note: outbound port 25 is
  blocked locally; 587 STARTTLS ("TLS" in mail-client terms, = `EnableSsl:true`) works.
- **Onboarding flow** (`OnboardingController` + `wwwroot/FRLScripts/Onboarding/CustOnboarding.js`):
  Udyam lookup → review → `RegisterUser` (rejects duplicate Email/Mobile with a popup +
  field clear) → email OTP → `FetchUserOTPDet` validates → session set → redirect to
  `/Dashboard/CustDashboard`. Controllers return a `redirectUrl` for the JS to follow.
- **Login** (`AuthController` + `wwwroot/FRLScripts/CustLogin/CustLogin.js`):
  `GenCustomerOtp` (unregistered email → toast + redirect to onboarding),
  `ValidateUserOtp`. OTP **expiry = 5 min**, checked in `FetchUserOTPDet` using
  `DateTime.UtcNow` (⚠ `SetAuditableFields()` stamps `UpdatedAt` in **UtcNow** — compare
  in UTC or every OTP reads as expired). Resend built into CustLogin.js (30s timer).
- **Session** (`Common/SessionExtensions.cs`): the logged-in MSME is stored as one JSON
  object under key `CurrentUser` (`UserSessionModel`: UserRegistrationID, MsmeEnquiryID,
  NameOfEnterprise, Email, MobileNumber, UdyamNumber, GstinNumber, PanNumber). Read
  anywhere via `HttpContext.Session.GetCurrentUser()` (or `Context.Session…` in views —
  imported in `_ViewImports`). `[CustDashboardAuthorize]` guards the dashboard off this.
  Cookie renamed **`frlai.sid`**, `HttpOnly`+`Secure`+`SameSite=Strict`. In-memory
  session/DataProtection keys are ephemeral (see gotchas).
- **CustDashboard** (`Views/Dashboard/CustDashboard.cshtml`): welcome banner (enterprise
  name + Udyam/GSTIN/PAN from session); GST/AA/ITR modals (static backdrop, blurred
  page, per-field validation before submit). `FinancialHealthCard` now takes UAN **from
  session only** (search box removed) — a user only ever sees their own card.

### 6. Dummy data + GST seeding (built 2026-07-05)
- `DummyDataService` (`IDummyDataService`) generates a random-but-valid Udyam response
  (valid PAN, **GSTIN with correct base-36 check digit**, mobile, state from the UAN
  token). `OnboardingController.FetchUdyam` uses it when the real Udyam lookup misses,
  and uploads `udyam.json` to the UAN's blob folder.
- Blob folder **`DUMMY-DATA`** holds a real 6-month GST fileset (36 files copied from
  `UDYAM-MH-20-0067394`). `Dashboard/SeedFinancialData` copies them into the logged-in
  user's folder, rewriting the template filer's GSTIN/PAN → the user's, and is a no-op
  if the folder already has `gstr*` files. Hooked to the dashboard "Fetch GSTR Details"
  button (validates password + consent first).
- Note: `IMsmeDataStore` upper-cases folder names, so `"dummy-data"` resolves to `DUMMY-DATA`.

### 7b. Secrets & config (2026-07-08 — repo going PUBLIC)
- **`appsettings.json` = PLACEHOLDERS only** (localhost / `123456` / `test-*` keys),
  safe to commit publicly. **Real values live in `appsettings.Development.json`**
  (same structure, git-ignored). Config layering loads Development.json only when
  `ASPNETCORE_ENVIRONMENT=Development` (all launch profiles set this) — VS/`dotnet run`
  pick it up automatically; a published/IIS run without the env var uses the
  placeholders. New machines: create `appsettings.Development.json` with the real
  values (ask Abhinav) — it is NOT in git.
- All keys were **rotated** on 2026-07-07 (SQL pwd, Azure storage key, JWT, SMTP,
  Finvu, Signzy, Groq, EncryptionKey). Old values in git history must be scrubbed with
  `git filter-repo --replace-text` before the repo is made public (see the security
  walkthrough; `ApplicationDbContextFactory.cs` also had a hardcoded conn string).
- ⚠ Gotcha that bit us: after rotating the SQL password, `appsettings.Development.json`
  still had the OLD one → `SqlException: Login failed for user 'FinRiskLensAI'`. That
  looked like "Development.json not loading" but config WAS loading — the credential
  was just stale. Keep Development.json's values current with the rotated secrets.

### 7c. New pieces since 07-06 (small)
- `MsmeDataFiles.Mca = "mca.json"` added to the known fixed files (MCA source slot).
- **GST Analysis page**: `Dashboard/GSTAnalysis` + `GSTAnalysisViewModel` + view, behind
  the sidebar "GST Details" link. Loads latest stored GSTR-2B/3B via
  `IGSTR2And3BResponceService.GetResponces()` (UAN from session). Phase 1 = data only,
  BI charts next.
- IP-risk service renamed/reshaped: `IIpRiskService.GetIpRiskScoreAsync(ip, uan)`
  (sources `m_StaticResponces.IPResponce` now, Signzy API when its key is live —
  `Signzy:*` config added, `UseApi=false`). Used by `Dashboard/GetIpVerificationDetail`
  for the "Secure Connection IP" popup; client IP is captured into session at login
  (`UserSessionModel.ClientIP`).
- `DashboardController` now injects `IGSTR2And3BResponceService` + `IDummyDataService`
  + `IIpRiskService` alongside the analysis/store/AA services.
- `AzureBlobDataStore` now builds its `BlobContainerClient` with retry + 60s network
  timeout (`BlobClientOptions`) so a brief CPU stall during ML warmup doesn't surface
  as a `TaskCanceledException` on the request path.

### 7. Account Aggregator — Finvu integration (built 2026-07-05)
- **API:** Finvu/FinFactor. Config `Finvu:*` in appsettings (`FinvuApi`,
  `AaUserId` `channel@dhanaprayoga`, `AaPassword`, `FinvuChannelId` `finsense`,
  `CallbackUrl` empty=auto). Bound to `FinvuSettings`, registered as an instance in
  `Program.cs` (same pattern as `Smtp`/`Groq`).
- `AccountAggregatorService` fully wired to Finvu (token login, `CreateConsentRequest`
  → `/ConsentRequestPlus`, `CheckConsentStatus`, FI request/status/fetch).
  `AccountAggregatorRepository` implemented against `t_AccAggreToken` (single-row upsert)
  and `t_AAConsentRequest`.
- **Consent flow:** dashboard "Fetch AA Details" → `Dashboard/InitiateAAFetch`
  (validates consent, `custId = mobile + "@finvu"`) → `CreateConsentRequest(uan, custId)`
  returns the Finvu consent URL → JS **opens it in a new tab** (opened synchronously
  pre-await to dodge popup blockers). Consent row saved with a fresh GUID `transactionId`
  that is ALSO embedded in the redirect URL, so the callback can find it.
- **Callback** (`Home/AAConsentCallback/{trnxid?}?ecres=…&resdate=…&fi=…`): looks up the
  consent by `trnxid`, calls `CheckConsentStatus(trnxid, custId)`, and renders success/
  failure (`Views/Home/AAConsentCallback.cshtml`) off the verified `Body.Status`
  (fallback: presence of `ecres`). ⚠ `custId` is persisted on the consent row precisely
  because the `SameSite=Strict` session cookie is NOT sent on Finvu's cross-site redirect
  — session is unavailable in the callback. Callback view: shows Transaction ID + Consent
  Handle + Consent Status only; "Return To Dashboard" hides the AA modal on the opener
  tab (window.opener) and closes itself, "Try Again" re-enables the Fetch button.
- **FI pipeline (2026-07-06):** when the callback sees `Status == "ACTIVE"` it calls
  `AccountAggregatorService.FetchAndStoreFinancialData(trnxid, consentDetails)` which runs
  FI request → status → fetch (gated on `errorCode == 0` at each step, FIDataRange from
  the consent detail) and writes the fetched JSON to the UAN's blob folder as **`aa.json`**
  (overwrite = update). Service was optimized: injected `ILogger` + `IMsmeDataStore`,
  a `GetValidTokenAsync()` helper dedupes the token dance, real structured logs + input
  validation on every method, the `CheckConsentStatus` `response2.Content` bug is **fixed**.
- Redirect/callback URL is dynamic: `ResolveCallbackUrl` uses `Finvu:CallbackUrl` if set,
  else builds `{scheme}://{host}/Home/AAConsentCallback/{trnxid}/` from the request
  (`IHttpContextAccessor`) — correct locally and after publish. Behind a proxy, set
  `Finvu:CallbackUrl` or enable forwarded headers.
- ⚠⚠ **AA data does not score yet.** `FinancialInfoFetch` stores the **raw** Finvu FIFetch
  response, whose FI objects are **encrypted**. `AaFeatureExtractor` needs *decrypted*
  data shaped `body[*].fiObjects[*]` (with `Summary`/`Transactions`), so it finds 0
  accounts → `HasAa=false` → AA contributes nothing to the score. `BlobAnalysisService.
  AnalyzeAsync` now logs a warning when `aa.json` is present but has no `fiObjects` node.
  Real fix = AA ECDH decryption before storing; demo fix = drop a plaintext `aa.json`
  (like the DUMMY-DATA GST trick). No FIStatus polling yet, so first-call PENDING → empty.

## Decisions & conventions (IMPORTANT)

- **Never add a Claude co-author line to commits.** Author is Abhinav only.
- Commits go **directly to main** (no PR flow); a teammate also pushes — pull
  `--rebase` before pushing.
- `Json files/` at repo root is **git-ignored on purpose**: it holds REAL personal
  data (PAN, Aadhaar, ITR, bank statements). Never commit it; sanitized samples
  would go elsewhere (e.g. `Doc/sample-payloads/`).
- **Secrets are OUT of `appsettings.json`** (placeholders committed; real values in
  git-ignored `appsettings.Development.json`) — see §7b. Repo is going public; scrub
  history before flipping it.
- Follow repo ground rules in `Doc/00_README.md` (strict layering, `*Repository`/
  `*Service` naming for Autofac auto-registration, decimals 18,4) — EXCEPT the
  entity-base rule, which changed on 2026-07-03: entities derive from
  `AuditableEntity` (audit fields only) and declare their own int identity PK
  named `<EntityName>ID`. `BaseEntity` no longer exists.

## Gotchas discovered (save yourself the debugging)

- PowerShell 5.1: `Get-Content -Raw` + `ConvertTo-Json` mangles JSON strings into
  objects (ETS metadata) → use `[IO.File]::ReadAllText(...)`. Also `$home` is a
  reserved read-only variable.
- The remote SQL server needs SQL auth (`user id=sa`) — `Trusted_Connection=True`
  fails with "untrusted domain".
- `dotnet ef` needed `Microsoft.EntityFrameworkCore.Design` in the WEB csproj
  (startup project), tool version 10.x works against EF Core 8.
- First call to a scoring endpoint pays ~1–2s of lazy LightGBM/PCA training; all
  later calls are instant.
- Real GST API responses are enveloped (`response.message.data`) — never parse
  GST payloads from the root without going through `GstFeatureExtractor.Unwrap()`.
- Missing data sources must feed NEUTRAL values into the ML feature vector, not
  zeros — a zero reads as worst-case behavior (this bit us: "no ITR" produced a
  bogus "ITR filed late" explanation until fixed).
- **Razor views are compiled into the DLL** (no runtime compilation). Editing a
  `.cshtml` and re-running with `--no-build` (or Ctrl+F5 without rebuild) serves the
  OLD view — always rebuild. (This masqueraded as "showToast not working": the toast
  markup existed on disk but the stale binary didn't have it.)
- **DataProtection keys — FIXED 2026-07-06.** Were in-memory/ephemeral → session cookies
  couldn't decrypt after an app restart → silent logout. This surfaced in production as
  "re-analyze → reload → logged out": the heavy first re-analyze (LightGBM/PCA training)
  recycles the IIS app pool, regenerating keys. `Program.cs` now persists keys to
  `App_Data/DataProtectionKeys` with `SetApplicationName("FinRiskLensAI")`. ⚠ prod: the
  app-pool identity needs WRITE access to `App_Data`; keys are unencrypted at rest
  (protect with a cert for real prod); use a SHARED path if you scale to multiple instances.
- Session store is now **SQL-backed** (2026-07-07): `AddDistributedSqlServerCache`
  → table `[FinRiskLensAI].[SessionCache]`, registered before `AddSession` in
  `Program.cs`. Fixes the prod bug where re-analyze → reload logged the user out:
  the heavy analyze recycled the app pool (or a farm switch), the in-memory session
  was wiped, and `CustDashboardAuthorize` redirected to Auth/CustLogin. SQL session
  survives recycles and multi-instance farms. The `GetCurrentUser()` API is unchanged.
  Create-table SQL (run once per DB; login's own schema, no dbo needed):
  ```sql
  CREATE TABLE [FinRiskLensAI].[SessionCache](
      [Id] nvarchar(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
      [Value] varbinary(max) NOT NULL,
      [ExpiresAtTime] datetimeoffset(7) NOT NULL,
      [SlidingExpirationInSeconds] bigint NULL,
      [AbsoluteExpiration] datetimeoffset(7) NULL,
      CONSTRAINT [pk_Id] PRIMARY KEY CLUSTERED ([Id] ASC));
  CREATE NONCLUSTERED INDEX [Index_ExpiresAtTime] ON [FinRiskLensAI].[SessionCache]([ExpiresAtTime]);
  ```
  (equivalent to `dotnet sql-cache create "<conn>" FinRiskLensAI SessionCache`).
  Table already created on the remote DB.
- **Analyze perf hardening (2026-07-07)** — the /analyze request was dying on the
  shared prod host ("TypeError: Failed to fetch", no result.json) because it (a)
  trained the ML models inline on first call and (b) downloaded 40+ blob files
  sequentially — slow enough to trip the reverse-proxy timeout / recycle the pool.
  Fixes: `MlWarmupService` (IHostedService in the ML project) pre-trains LightGBM +
  PCA at startup off the request path (`ScoreCalibrationModel.Warmup()` /
  `IncomeAnomalyDetector.Warmup()`); `BlobAnalysisService.AnalyzeAsync` now downloads
  all files in parallel (SemaphoreSlim(8)). Warm-path analyze is ~3s locally.
- Timestamps: `AuditableEntity` audit fields are stamped **UtcNow** by
  `SetAuditableFields()` on every save (overrides whatever you set). Any time-window
  check (e.g. OTP expiry) MUST compare in UTC. Non-audit models (AA token/consent) keep
  their own `DateTime.Now` fields — those compare in local time.
- `SameSite=Strict` session cookie is not sent on cross-site redirects back to us
  (e.g. Finvu AA callback) — don't rely on session in redirect landing pages; carry
  what you need in the URL / persisted row instead.

## Likely next steps (not started)

1. **AA data → score (the big open item):** the FI pipeline + `aa.json` write are DONE
   (§7), but the stored FI data is encrypted so it doesn't score. Add AA ECDH decryption
   (write decrypted `body[*].fiObjects[*]` to `aa.json`), OR for the demo generate/seed a
   plaintext AA fileset like DUMMY-DATA GST. Also add FIStatus polling (first call is
   usually PENDING → empty fetch). Watch the `BlobAnalysisService` "no fiObjects" warning.
2. Confirm the ConsentStatus/FI endpoints' AAID param against the Finvu sandbox —
   code passes `custId` (`mobile@finvu`); if it should be the AA handle
   (`cookiejar-aa@finvu.in`), switch it. Also the FI pipeline runs INLINE in the callback
   (3 sequential Finvu calls block the page) — move to a background task if too slow.
3. Admin login controller/service/UI against `ADM_Login` (+ JWT bearer registration —
   gap #2 in `Doc/07_SETUP_GAPS.md` still open).
4. EPFO extractor when the data source arrives (slot exists: `EpfoJson`,
   `epfo.json`, `HasEpfo`).
5. Map `RiskAnalysisResult` onto `ScoreComputation`/`ScoreExplanation`/
   `CreditProductRecommendation` entities (not created yet) per `Doc/01_DOMAIN_MODEL.md`;
   then add the score **trend line** to the Financial Health Card.
6. Swap synthetic training data for IDBI sandbox data when it opens **July 22**.
7. Anomaly-detector thresholds need tuning with realistic data (current test mixed
   one company's GST with personal ITR/AA, so cross-source figures were incoherent).
8. **Pre-deploy hardening:** ~~persist DataProtection keys~~ (done — protect them with a
   cert / shared path for multi-instance), move secrets out of `appsettings.json`, and
   replace the dummy-data / GST-seeding demo shortcuts with real pulls.
