# FinRiskLensAI — Documentation Index

FinRiskLensAI is an MSME Financial Health Score platform, built for IDBI
Innovate 2026 (Problem Statement 3). It aggregates alternate data (GST, ITR, AA
bank statements, UPI patterns, EPFO payroll) for New-to-Credit (NTC) and
New-to-Bank (NTB) MSMEs, computes a 6-dimension weighted credit health score,
and exposes it to IDBI's Loan Origination System and to the broader
ULI/OCEN lending ecosystem.

The codebase already exists as a scaffold — see `ARCHITECTURE.md` (provided
separately) for the current solution structure, layering, and DI setup.
These documents describe the *business flows* and *build order* on top of
that scaffold. They intentionally leave implementation detail (exact method
signatures, exact LINQ, exact Razor markup) to you — that's the point of
handing this to Claude Code rather than writing it all by hand.

## Reading order

1. `01_DOMAIN_MODEL.md` — entities, relationships, what each layer owns
2. `02_CUSTOMER_FLOW.md` — the end-to-end MSME journey, step by step
3. `03_SCORING_ENGINE.md` — how the 6-dimension score is computed
4. `04_API_CONTRACTS.md` — internal + external API surface (ULI/OCEN, LOS)
5. `05_INTEGRATIONS.md` — AA, GST, ITR, EPFO, Udyam — what each one is
   and how
   we talk to it
6. `06_BUILD_PLAN.md` — suggested build order, mapped to the hackathon
   timeline
7. `07_SETUP_GAPS.md` — the two known gaps in the current scaffold
   (DbContext registration, JWT handler) plus PostgreSQL provider wiring
8. `08_ML_ENGINE.md` — the implemented ML.NET scoring engine
   (`FinRiskLensAI.ML`): use cases, API contract (direct endpoint + the
   Azure Blob Storage collection flow for large payloads), pipeline
   internals with code, and extension points (EPFO, sandbox swap, bureau)
9. `09_FINALS_ENHANCEMENT_PLAN.md` — the top-5 finals build plan: verified
   prototype state, the AA scoring gap, tiered priorities with effort
   estimates, and what deliberately not to build

## Ground rules for implementation

- Follow the existing layering strictly: `Core` has no dependencies,
  `Data` depends only on `Core`, `Services` depends on `Core` + `Data`,
  `Web` depends on `Core` + `Services`. Do not let `Web` call `Data`
  directly, and do not let `Core` reference EF Core or any infrastructure
  package.
- New repositories end in `Repository` and implement an interface — they
  auto-register via `DataModule`. New services end in `Service` and
  implement an interface — they auto-register via `ServicesModule`. Don't
  hand-register anything that fits this convention.
- ~~All entities derive from `BaseEntity` or `AuditableEntity`.~~
  **Changed 2026-07-03:** `BaseEntity` was removed. Entities derive from
  `AuditableEntity` (audit timestamps/actors only) and each declares its own
  **int identity primary key named `<EntityName>ID`** (e.g. `MsmeEnquiryID`) —
  no Guid keys, no shared `Id`, no `IsDeleted`/soft-delete.
- Money and score fields are `decimal`, relying on the existing
  `precision(18,4)` convention — don't override precision per-field
  without a reason.
- This is a hackathon build against a July 31, 2026 deadline. Prefer
  working, demonstrable slices over exhaustive edge-case handling. Where a
  document below says "synthetic data" — that means the IDBI sandbox
  isn't available yet, so build against a local fixture/generator first
  and swap the data source when sandbox access opens.
