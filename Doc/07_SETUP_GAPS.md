# Setup Gaps — Fix These First

> **Implementation status (2026-07-02):**
> - **Gap 1 (DbContext) — CLOSED**, but with a decision change: **SQL Server is
>   the actual target**, not PostgreSQL (see gap 3 note below). Registered via
>   `DataServiceCollectionExtensions.AddAppDbContext()` in the Data project,
>   called from `Program.cs`, driven by `Database:DbType`. The Npgsql branch
>   exists in code but is unused. Migrations live in
>   `FinRiskLensAI.Data/Migrations/SqlServer/` and are applied to the remote
>   shared SQL Server (connection string in `appsettings.json`). Migration
>   commands are documented in `DB_SCRIPTS.md`.
> - **Gap 2 (JWT bearer) — STILL OPEN.** Nothing registers
>   `AddAuthentication().AddJwtBearer(...)` yet; the `Jwt` config section
>   remains inert. Needed before the external API surface
>   (`04_API_CONTRACTS.md` §3/§4) ships.
> - **Gap 3 (PostgreSQL as target) — DECISION REVERSED.** The team chose SQL
>   Server (a remote instance; host in `appsettings.Development.json`). Do NOT
>   regenerate migrations
>   for Npgsql; the guidance below is retained for history only.

`ARCHITECTURE.md` §11 flags these explicitly. Listing them here as a
concrete first task for Claude Code, plus the PostgreSQL provider
decision that touches the same area of `Program.cs`.

## 1. DbContext is not registered with a provider

`ApplicationDbContext` exists (`FinRiskLensAI.Data/DbContextEDMX/`) but
there's no `AddDbContext` call in `Program.cs`, so nothing can resolve
it yet — repositories will fail at runtime until this is wired.

**What's needed:**
- Read the `Database:DbType` setting from `appsettings.json` (already
  present per the architecture doc).
- Given this project is standardizing on PostgreSQL (see tech stack),
  register the Npgsql provider as the default path:
  `options.UseNpgsql(connectionString)`, sourced from the PostgreSQL
  named connection string already anticipated in config.
- Keep the SQL Server branch available behind the same `DbType` switch
  if the config already anticipates it — don't rip it out, just make
  sure PostgreSQL is what actually gets used by default for this build.
- Add the `Npgsql.EntityFrameworkCore.PostgreSQL` package reference to
  `FinRiskLensAI.Data` if it isn't already present (the architecture doc
  currently lists the SQL Server package explicitly, PostgreSQL is
  listed only as "ready").

## 2. JWT bearer handler is not registered

The `Jwt` config section exists and middleware ordering already
includes `UseAuthentication()`/`UseAuthorization()`, but there's no
`AddAuthentication().AddJwtBearer(...)` call, so the configured JWT
settings are currently inert.

**What's needed:**
- `AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
  .AddJwtBearer(...)` in `Program.cs`, reading issuer/audience/signing
  key from the existing `Jwt` config section.
- This becomes the auth mechanism protecting the external API surface
  described in `04_API_CONTRACTS.md` (ULI DSP API, IDBI LOS webhook) —
  those endpoints should require a valid bearer token once this is
  wired. (The *outbound* loan-case push in §4a already runs — it is
  bank-session authenticated, and sends its own optional Bearer key
  downstream, so it does not depend on this gap.)
- The dev signing key currently in `appsettings.json` is a placeholder
  per the architecture doc's own warning — fine for local dev, but
  don't let it leak into anything demo-facing as if it were real;
  mention in the demo/README that production would move this to a
  secret store.

## 3. Decide and confirm: PostgreSQL as the actual target

The architecture doc describes the codebase as "SQL Server provider;
PostgreSQL-ready" — this build should flip that to PostgreSQL as
primary (per the updated tech stack). Concretely:

- Local dev connection string should point at a local/dev PostgreSQL
  instance (or Azure Database for PostgreSQL Flexible Server if
  developing against a cloud instance directly).
- Confirm the existing `decimal(18,4)` precision convention in
  `ApplicationDbContext` behaves correctly under Npgsql — this is
  generally fine but worth a quick sanity check with a migration + a
  test write, since decimal handling is one of the few places Npgsql
  and SQL Server providers can differ.
- EF Core migrations should be regenerated (or generated fresh, if none
  exist yet) against the Npgsql provider once the above is wired —
  don't reuse SQL-Server-generated migrations against Postgres.

## Suggested order

Do all three together as one setup pass before Phase 1 of
`06_BUILD_PLAN.md` — they all touch the same `Program.cs` region and
are easiest to verify in combination (a successful migration + a
successful authenticated request confirms both are working).
