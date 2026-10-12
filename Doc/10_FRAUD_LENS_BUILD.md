# 10 — FraudLens Build Plan

Turning this codebase into **FinRiskLens — evidence-based fraud detection + fraud
ring discovery** for the Google AI Builder Cup 2026 (BFSI track).

> **Submission: Oct 18, 2026. Target: Oct 16. Team registration closes Oct 11.**
> This plan starts Sep 29 — roughly **18 working days**. The reference design's own
> 4-week plan has already lost its first week, so everything below is compressed
> and carries an explicit cut list.

Companion documents: [`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md) (retrieval
design in full), [`ARCHITECTURE.md`](ARCHITECTURE.md) (current layering),
[`DB_SCRIPTS.md`](DB_SCRIPTS.md) (schema log — every new table goes there first).

---

## 1. What we are building, and what changes

The existing product scores MSMEs from consented API data across six dimensions.
The new product reads **uploaded loan documents** and returns **evidence-backed
fraud findings**, then links cases together to expose **fraud rings**.

Different input, different output, different user moment. The **platform** carries
over; the **scoring** does not.

### The data model shift that matters

| | Role |
|---|---|
| **Uploaded documents** (statements, slips, KYC, ITR) | **Primary evidence** — what is under investigation |
| **Registry APIs** (MCA, GST, ITR) + **Account Aggregator** | **Verification sources** — what we check the uploads *against* |
| **Knowledge corpora** (policy, regulation, typology, case memory) | **Judgement context** — see [`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md) |

This three-way split is the differentiator. The reference design has only the first,
because it assumes a team starting with nothing but a file-upload box. We already
own the second, and the third is what the RAG layer adds.

---

## 2. Keep

| Area | Files | Becomes |
|---|---|---|
| Layering + DI conventions | `Core`, `Data`, `Services`, `ServicesModule`, `DataModule` | Unchanged — new checks and agents auto-register |
| EF Core plumbing | `ApplicationDbContext`, `Configurations/`, `AuditableEntity` | Unchanged |
| Bank portal | `BankAdminController`, `Views/BankAdmin/*`, `BankAdminAuthorize`, `_BankAdminLayout` | Cases Inbox + Case Report |
| UI patterns | `.detail-modal` in `Dashboard.css`, `panel-tabs.js`, `Views/LoanCase/Payload.cshtml` | Finding cards, evidence viewer, JSON viewer |
| Storage abstraction | `IMsmeDataStore` + `MLModule` registration | GCS swap = one class, one DI line |
| LLM call pattern | `ChatbotService` (Groq via `IHttpClientHelper`) | Template for `GeminiClient` |
| **Account Aggregator** | `AccountAggregatorController`, `AccountAggregatorService`, `AaStatementAnalysisService`, `SimBankAccountService`, Finvu config | **Statement truth source** |
| **MCA** | `McaController` + MCA models/services | **Employer verification** |
| **ITR / GST fetch** | `ItrController`, `GSTR2And3BResponceService` | Cross-check against uploaded filings |
| **Udyam** | `UdyamController` | Applicant business identity (SME cases) |
| Synthetic response infra | `StaticResponseService`, `DummyDataService` | Already serves synthetic registry responses in documented API shapes — which the hackathon **requires** |
| Utility | `IHttpClientHelper`, `EncryptionService`, `IpRiskService`, `EmailService`, `SecurityHeaders`, session infra | Unchanged |
| Loan-case push | `LoanCaseService` + 3 payload builders | The reference design's v5 "API to LOS", already built |

---

## 3. Remove

Scoring and its presentation only. **Not** the data acquisition.

```
ML/MachineLearning/*            LightGBM, SSA, PCA, synthetic profile generator
ML/Services/                    RiskScoringService, LendingCalculator, MlWarmupService
Controllers/                    Scoring, ScoreHistory, Report
Views/                          Dashboard/FinancialHealthCard, Report/
wwwroot/                        score-gauge.js, score-history.js
Data/Configurations/            MsmeScoreSummary, MsmeScoreHistory
Core/Models/Scoring/
```

**Packages dropped:** `Microsoft.ML`, `Microsoft.ML.LightGbm`, `Microsoft.ML.TimeSeries`,
`Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.EntityFrameworkCore.SqlServer`,
`Microsoft.Extensions.Caching.SqlServer`, `Azure.Storage.Blobs` (removed 2026-10-12, once
both stores were on Cloud Storage — the app has no Azure dependency left).

**`Npgsql.EntityFrameworkCore.PostgreSQL` is already referenced** and stops being
dead weight — it becomes the provider. Add `Pgvector.EntityFrameworkCore` alongside
it.

> ⚠ **The session store needs a decision.** Sessions are SQL-backed today via
> `AddDistributedSqlServerCache` and the `SessionCache` table (`DB_SCRIPTS.md`,
> 2026-07-07), which exists because app-pool recycles were wiping in-memory sessions.
> There is no first-party PostgreSQL distributed-cache provider. On Cloud Run with
> `--session-affinity` and `--min-instances=1`, in-memory sessions are fine for the
> hackathon — take that, and note the ceiling rather than adding Memorystore.

**Kept — all four feature extractors** (`Udyam`, `Gst`, `Itr`, `Aa`) and the deep-dives.
*Corrected 2026-09-30:* an earlier draft listed the GST, ITR and Udyam extractors for
removal as "score features". They are the **parsers for the verification sources** — the
bounce counts, ECS returns, GST filing rates, ITR net worth and GST-vs-ITR variance that
the fraud findings are built from. Deleting them repeats the mistake of deleting the
registry fetch. They now feed `SourceFactsService` (extract → deep-dives, no score), and
a golden test pins its output to what the V1 scorer produced for all four demo accounts.

> ⚠ **For Phase 3's `INCOME_MISMATCH`:** compare `ItrBusinessTurnover`, never
> `ItrMonthlyAvgIncome`. The latter is gross total income — roughly profit — so setting
> it against GST turnover measures the margin, not a mismatch. V1's anomaly check did
> exactly that, which is one reason its verdicts were meaningless. Found and fixed in the
> loan-case payloads during Phase 1.

**`FinRiskLensAI.ML` shrinks to almost nothing.** Fold what survives into
`FinRiskLensAI.Fraud` and delete the project.

---

## 4. Add

### New project: `FinRiskLensAI.Fraud`

```
FinRiskLensAI.Fraud/
├─ Ai/
│  ├─ GeminiClient.cs              modelled on ChatbotService
│  ├─ DocumentExtractor.cs         Flash, structured output, bounding boxes
│  ├─ Investigator.cs              function-calling loop, 8-turn cap
│  ├─ InvestigatorTools.cs         scoped server-side to applicationId
│  ├─ CounterpartyClassifier.cs    batched, cached
│  ├─ LayoutFingerprinter.cs       layout text → embedding
│  └─ RingAnalyst.cs
├─ Pipeline/
│  ├─ CasePipelineWorker.cs        BackgroundService + Channel<T>
│  ├─ IngestStep.cs                GCS upload, SHA-256, PdfPig metadata
│  ├─ DeterministicChecks.cs       the seven SQL checks
│  ├─ GroundingVerifier.cs         + AiDropLog
│  ├─ AaCrossCheck.cs              ★ uploaded statement vs AA-sourced truth
│  ├─ RegistryCrossCheck.cs        ★ slip employer vs MCA, ITR PDF vs filed ITR
│  └─ NetworkLens.cs               fingerprints, graph edges, union-find
├─ Rag/                            see 11_RAG_ARCHITECTURE.md §15
└─ Storage/GcsDocumentStore.cs
```

### New tables
`applications`, `documents`, `statement_rows`, `page_summaries`, `doc_fields`,
`findings`, `evidence`, `rings`, `ai_drop_log`, the link tables that replace SQL
Server's graph tables (`entities`, `application_entities`, `document_templates`,
`application_links`), and the RAG tables (`knowledge_docs`, `knowledge_chunks`,
`chunk_embeddings`, `rag_query_log` — see
[`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md) §14).

Snake-case naming is the Npgsql/EF Core default and is kept rather than fought.

Every one gets an entry in [`DB_SCRIPTS.md`](DB_SCRIPTS.md) **before** the migration runs.

### New controllers / views
`CasesController` (inbox, upload, report), `RingController` (explorer),
`FindingReviewController` (confirm / dismiss).

### New test project
There is none today. `DeterministicChecksTests`, `GroundingVerifierTests`,
`UnionFindTests`, `ChunkerTests`, `RagGroundingTests`.

### New tooling
`tools/DemoDataGenerator` (QuestPDF synthetic documents), `Dockerfile`, `deploy.sh`,
`db/graph.sql`, `db/seed_knowledge.sql`.

### Findings unlocked by keeping the registry sources

| Finding | Reference design | Ours |
|---|---|---|
| `IMPLAUSIBLE_EMPLOYER` | "AI reasoning", stretch goal | **Deterministic** — MCA says the company does not exist or was struck off |
| `EMPLOYER_INACTIVE` | not present | MCA status ≠ Active |
| `ITR_MISMATCH` | uploaded ITR vs uploaded slip | uploaded ITR vs **filed** ITR |
| `STATEMENT_VS_AA_MISMATCH` | deferred to v2 | **ships in v1** |

---

## 5. Google Cloud

### Services to enable
| Service | API | Purpose |
|---|---|---|
| Vertex AI | `aiplatform.googleapis.com` | Gemini Pro + Flash + embeddings |
| **Cloud SQL for PostgreSQL** | `sqladmin.googleapis.com` | Main DB — app data, knowledge chunks and vectors in one place. Needs the **`pgvector`** extension |
| Cloud Storage | `storage.googleapis.com` | Case documents + knowledge bucket |
| Cloud Run | `run.googleapis.com` | Hosting, session affinity |
| Artifact Registry | `artifactregistry.googleapis.com` | Images |
| Cloud Build | `cloudbuild.googleapis.com` | `gcloud run deploy --source .` |
| Secret Manager | `secretmanager.googleapis.com` | Connection string, Finvu creds |
| Service Networking + Compute | `servicenetworking.`, `compute.googleapis.com` | Private IP for Cloud SQL |
| Cloud Logging | (auto) | Serilog sink |

### Service account `finrisk-run`
`aiplatform.user` · `cloudsql.client` · `secretmanager.secretAccessor` ·
`storage.objectAdmin` on both buckets · `iam.serviceAccountTokenCreator` on itself
(to sign URLs without key files).

### Two warnings
- **Cloud SQL still has no free tier**, but PostgreSQL carries no licence component
  in the instance price, so the smallest usable size is materially cheaper than the
  SQL Server equivalent. **Stop the instance when not working** and the $300 trial
  credit covers the project comfortably.
- **Gemini may not serve from `asia-south1`.** Documents stay in Mumbai; AI calls
  may route via `global`. Fine for synthetic data — say so in the deck rather than
  letting a judge find it.

### Why PostgreSQL and not SQL Server

The project runs on SQL Server today, so this is a deliberate reversal — recorded
here because `07_SETUP_GAPS.md` documents the team choosing SQL Server originally.

| | SQL Server on Cloud SQL | PostgreSQL on Cloud SQL |
|---|---|---|
| Vectors | No `VECTOR` type — that is SQL Server 2025, not offered. Vectors stored as blobs, distance computed in C# | **`pgvector`** — real vector column, HNSW index, distance in SQL |
| Full-text (the sparse retrieval arm) | Optional install feature; **availability on Cloud SQL must be verified** | Core engine — `tsvector`/GIN, nothing to enable |
| Price | Licence cost inside the instance price | No licence component |
| Graph tables | `AS NODE` / `AS EDGE` / `MATCH` | Not available — see below |
| Migration cost | Zero | Regenerate EF migrations for Npgsql |

Hybrid retrieval is load-bearing for policy lookups, and on SQL Server its keyword
arm depends on a feature we cannot confirm is present. That alone decides it.

**Losing graph tables costs little.** They were syntactic sugar for link-discovery
queries that are ordinary joins underneath, and the clustering itself was always
specified as union-find in C#. The `gApplication` / `gEntity` / `gTemplate` node and
edge tables become plain tables with foreign keys; the `MATCH` queries become joins
or a recursive CTE.

**The migration cost is smaller than it looks**, because the schema is being rebuilt
anyway: scoring tables deleted, ten new tables added, fresh database. What changes is
the dialect of the raw SQL in the deterministic checks — `ISNULL` → `COALESCE`,
`DATEADD` → interval arithmetic. Window functions are identical.

### Local development
`gcloud auth application-default login` — no key files in the repo, ever.
Local PostgreSQL with `pgvector` for development; the connection string slot for it
already exists in `appsettings.json`.

---

## 6. Project updates

**Packages added:** `Google.Cloud.AIPlatform.V1` (or `Google.GenAI`) ·
`Google.Cloud.Storage.V1` · `Pgvector.EntityFrameworkCore` · `UglyToad.PdfPig` ·
`Dapper` · `QuestPDF` (tools only) · `xunit` + `Microsoft.NET.Test.Sdk`

**Migrations are regenerated for Npgsql.** The existing ones live in
`Migrations/SqlServer/` and are provider-specific. Target is a fresh database with a
substantially new schema, so this is a clean baseline under `Migrations/Postgres/`,
not a conversion. Keep the SQL Server folder in git history; delete it from the tree.

**Raw SQL needs dialect changes** in the deterministic checks: `ISNULL` →
`COALESCE`, `DATEADD(DAY, 3, d)` → `d + INTERVAL '3 days'`, `TOP n` → `LIMIT n`.
Window functions (`LAG() OVER`) are identical.

**`IMsmeDataStore` must change.** It is string-only today:
```csharp
Task UploadAsync(string uan, string fileName, string content, ...);
Task<string?> DownloadAsync(...);
```
PDFs are binary. Rename to `ICaseDocumentStore`, add byte overloads and
`Task<string> GetSignedUrlAsync(...)` for the PDF viewer.

**`appsettings.json`:** remove `AzureBlob`; add `Gcp`, `Gcs`, `Gemini`, `Fraud`,
`Rag` (see [`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md) §16). Keep `Finvu`,
`Smtp`, `Jwt`, `Serilog`. Under `Database:ConnectionStrings`, **PostgreSQL becomes
the live entry and the SqlServer entry is removed** — the slot already exists.
`Groq` goes once Gemini replaces the chatbot.

**Stays on .NET 8 + MVC.** Upgrading to .NET 10 and rewriting the UI in Blazor costs
a week we do not have, and neither is required by the rules. Live pipeline progress
comes from SignalR directly.

---

## 7. Phases

Two lanes run in parallel. **The RAG lane (R-stages) has no UI dependency through
R7** and is developed against a CLI harness while the case pipeline is built.

### Phase 0 — Gates · Sep 29 (half day)
- **Register the team.** Free, and the only irreversible deadline (Oct 11)
- GCP project, billing on, APIs enabled, service account, region confirmed
- Settle: .NET 8 + MVC, `FinRiskLensAI.Fraud` as the new project, scoring removal approved
- **Verify `CREATE EXTENSION vector;` succeeds on your Cloud SQL PostgreSQL version.**
  Everything in `11_RAG_ARCHITECTURE.md` sits on top of it, so confirm it before
  writing a line of retrieval code

### Phase 1 — Prove the platform on GCP · Sep 30 – Oct 2
| Lane | Work |
|---|---|
| Main | `GcsDocumentStore` · `GeminiClient` · **Cloud SQL for PostgreSQL provisioned, `CREATE EXTENSION vector` verified** · EF provider switched to Npgsql, baseline migration generated and applied · **existing app deployed to Cloud Run** |
| RAG | **R1** — knowledge schema + `pgvector` + HNSW and GIN indexes, logged in `DB_SCRIPTS.md` |

> Ship nothing new until the current app runs on Google's infrastructure. Three days
> that de-risk everything after them.

**Status 2026-09-30 — database and deletions done; GCP pieces blocked on a project.**

| Item | Status |
|---|---|
| Scoring engine removed (features A1–A9) | ✅ Health Card, report, score history, scoring API, ML.NET, bank-portal score UI |
| Facts split from score | ✅ `SourceFactsService`; golden test 5/5 against the V1 scorer |
| Loan-case push on facts | ✅ LOS / ULI / ONDC payloads carry verified facts, no score; frozen score columns dropped |
| EF provider → Npgsql, snake_case, legacy timestamps | ✅ `PostgresSetup` — one config shared by runtime and migrations |
| Baseline migration applied | ✅ on **Neon** (dev), verified from the database |
| **R1** knowledge schema | ✅ pgvector 0.8.6, HNSW + GIN, both retrieval arms smoke-tested |
| Sessions | ✅ in-memory (ceiling documented in `Program.cs`) |
| App runs against PostgreSQL | ✅ bank login, dashboard, customer list, Customer 360, all three payload previews — no server or console errors |
| Fork isolated from V1's live blob data | ✅ own container `msme-data-rag-v2` |
| GCP project | ✅ 2026-10-12 — `finrisklensai`; Vertex AI, Cloud Run, Artifact Registry, Cloud Build, Secret Manager, Cloud SQL Admin enabled; service account `finrisk-run` with least-privilege roles; private buckets `finrisklensai-cases` and `finrisklensai-knowledge` in `asia-south1` |
| `GcsDocumentStore` · `GeminiClient` | ✅ 2026-10-12 — case documents in Cloud Storage (Mumbai), the 15 earlier files copied across; Vertex AI over REST with Application Default Credentials |
| `GcsMsmeDataStore` | ✅ 2026-10-12 — the verification sources (Udyam, GST, ITR, AA payloads) in `gs://finrisklensai-sources`, Mumbai: 91 files copied and each compared on read-back. Customer 360, loan-case payloads and the Investigator's facts tool read from it. Azure store, package and settings removed; the Azure container was left untouched |
| Cloud Run deploy | ✅ 2026-10-12 — **https://finrisklensai.cloud.run** (also https://finrisklens-389451907343.asia-south1.run.app), Mumbai. Built from source by Google Cloud buildpacks (no Dockerfile). See "Deploying" below |
| Cloud SQL for PostgreSQL | ⏸ Neon stands in for development; switching is a connection-string change |

### Deploying

One command from the repository root — Cloud Build runs the .NET buildpack on the web
project and Cloud Run starts it:

```bash
gcloud run deploy finrisklens --source . --region asia-south1 --project finrisklensai --service-account finrisk-run@finrisklensai.iam.gserviceaccount.com --allow-unauthenticated --min-instances 1 --max-instances 1 --no-cpu-throttling --session-affinity --memory 1Gi --cpu 1 --timeout 300 --set-build-env-vars GOOGLE_BUILDABLE=FinRiskLensAI-RAG-V2/FinRiskLensAI-RAG-V2.csproj --set-env-vars ASPNETCORE_HTTP_PORTS=8080,ASPNETCORE_FORWARDEDHEADERS_ENABLED=true,DataProtection__KeysPath=/tmp/dp-keys --set-secrets /secrets/appsettings.secrets.json=finrisklens-settings:latest
```

| Setting | Why |
|---|---|
| `--min-instances 1 --max-instances 1 --session-affinity` | Sessions and the case queue are in memory: exactly one warm instance |
| `--no-cpu-throttling` | Extraction runs after the upload request returns; a throttled CPU would stall it. Costs more than request-only CPU — scale down after judging |
| `GOOGLE_BUILDABLE` | The solution has several projects; this names the web app |
| `ASPNETCORE_HTTP_PORTS=8080` | The buildpack image does not set a port; Kestrel's default (5000) fails Cloud Run's health check |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | TLS ends at Google's front end: trust `X-Forwarded-Proto/-For` so HTTPS-only cookies work and client IPs are real |
| `DataProtection__KeysPath=/tmp/dp-keys` | The app folder is read-only on Cloud Run. Keys are per-instance, so a new revision signs everyone out once |
| `/secrets/appsettings.secrets.json` | Secret `finrisklens-settings` (Mumbai-only replication): database, encryption key, SMTP, Finvu, Groq, Signzy, loan-case keys and `MsmeData:IngestKey`, built from the needed sections of the local development settings. Version 2 since 2026-10-12 (Azure and JWT sections dropped); version 1, which held the Azure key, is disabled. `Program.cs` loads the file when present |

Found on the first deploy:
- **Cloud Build needs `roles/cloudbuild.builds.builder` on the default compute service
  account** in new projects, or it cannot read its own uploaded source.
- **Linux paths are case-sensitive.** Eight views referenced scripts and stylesheets in the
  wrong case (the bank login script among them) — fine on Windows, 404 on Cloud Run. Fixed.
- **The MSME data ingest API had no authorisation.** On a public URL anyone could plant
  registry or bank data for any Udyam number — the data uploads are verified against. It
  now requires `X-Ingest-Key`; with no key configured it is closed.
- **Logs go to the console only** in the base settings; Cloud Run sends them to Cloud
  Logging. A file sink would grow inside the instance's in-memory filesystem.

### Phase 2 — Case ingest + extraction · Oct 3 – Oct 6
| Lane | Work |
|---|---|
| Main | Case schema + migration · upload screen · PdfPig metadata · `DocumentExtractor` (Flash, boxes) · `CasePipelineWorker` |
| RAG | **R2** chunkers + unit tests · **R3** `EmbeddingClient` + ingest the typology corpus (smallest, cleanest) |

**Status 2026-10-02 — everything that does not need Gemini is done.**

| Item | Status |
|---|---|
| New project `FinRiskLensAI.Fraud` | ✅ in the solution, `FraudModule` registers `*Service` by convention |
| Case schema + migration | ✅ `20261002102310_CaseSchema` on Neon — `applications`, `documents`, `statement_rows`, `page_summaries`, `doc_fields` (logged in `DB_SCRIPTS.md`) |
| Upload screen | ✅ Bank portal → **Cases**: inbox, open-a-case form, case view, inline document viewer |
| Ingest | ✅ type sniffed from the bytes (PDF/PNG/JPEG only), SHA-256, content-addressed storage `cases/{sha256}{ext}`, PdfPig metadata (producer, creator, dates, fonts, pages) |
| Cross-case duplicate files | ✅ shown on the case view — the cheapest cross-application link, ahead of the network lens |
| **R2** chunkers | ✅ `MarkdownChunker` — one splitter configured per corpus; 8 tests incl. no mid-clause split, tables whole, overlap by whole clauses |
| `DocumentExtractor` | ✅ 2026-10-12 — `gemini-3.5-flash` in **Mumbai**, page by page in parallel. Measured against the demo truth (below) |
| **R3** embeddings | ✅ 2026-10-12 — `gemini-embedding-001`, 768 dims, normalised, in Mumbai; new chunks embedded on ingest, `RagCli embed` backfills |
| Extractor output contract | ✅ `ExtractedDocument` (pages → rows, fields) and `ICaseService.ApplyExtractionAsync` — replaces a document's rows, pages and fields and marks it Done. The Gemini extractor only has to produce this shape; the demo generator already does |
| `CasePipelineWorker` | ✅ 2026-10-12 — upload → extract → checks → Review off the request thread; re-queues anything a restart interrupted; a document Gemini cannot read is marked Failed with the reason, the rest carry on. Live: a two-document case in 13 s |

**Extraction measured, not assumed** (`DemoData eval`: live Gemini against the generator's truth files, 12 documents, 895 rows):

| | One call per document | **Page by page, in parallel** |
|---|---|---|
| Rows exact (date, debit, credit, balance all right) | 88.8% | **100% (895/895)** |
| Page balances and totals | 100% | **100% (172/172)** |
| Slip fields | 100% | **100% (16/16)** |
| Cases whose findings match expectation | 7/10 | **10/10** |
| Time per statement | 30–72 s; one gave up after 215 s | **10–25 s** |

The single-call failures are the instructive part: Gemini dropped 45 of 84 rows from one
genuine statement, which broke its arithmetic and produced **false tamper findings on an
honest applicant**, and an 8-page statement ran out of output tokens. Splitting into pages
— the reference design's own advice — removed both. Gemini also kept a fabricated slip's
wrong net pay as printed rather than "correcting" it, which is what lets the check fire.

Notes from the build:
- **PdfPig's NuGet id is `PdfPig`.** `UglyToad.PdfPig` on nuget.org belongs to an
  unrelated, unverified owner and ships odd versions — do not add it.
- **Password-protected PDFs** (most Indian bank e-statements) still upload; their
  metadata is empty and the case view says "unreadable". The extractor will need the
  password path.
- **Binary storage** is a second interface, `ICaseDocumentStore`, implemented by the
  existing Azure store in the fork's own container. The GCS store implements both.

### Phase 3 — Deterministic findings + Case Report · Oct 7 – Oct 9
| Lane | Work |
|---|---|
| Main | Seven SQL checks + tests · Case Report UI · PDF.js evidence highlighting |
| RAG | **R4** `SqlVectorIndex` + dense retrieval + CLI harness · **R5** sparse arm + RRF fusion |

> **End of Phase 3 is the insurance line:** a demoable product with zero AI risk.
> If everything after this fails, there is still something to show.

**Status 2026-10-03 — main lane done; it fires on real data once the extractor fills the rows.**

| Item | Status |
|---|---|
| `findings` + `evidence` tables | ✅ `20261003170922_Findings` on Neon, logged in `DB_SCRIPTS.md` |
| Deterministic checks | ✅ eight, in `Fraud/Checks/DeterministicChecks.cs`: balance continuity, page carry-over, stated totals, slip arithmetic, round-tripping, window dressing, pass-through, PDF metadata. 13 tests, one hand-made statement per check plus a clean one that must raise nothing |
| Case Report UI | ✅ findings on the case view, most severe first — explanation in rupees, next step, every evidence row linking to its page in the document |
| Review | ✅ confirm / dismiss / reopen with a note. Re-runs replace open findings and never resurrect a reviewed one |
| When checks run | ✅ on upload (metadata needs nothing else) and from **Re-run checks**; the extractor will call the same entry point |
| PDF.js box highlighting | ⏸ needs the boxes the extractor writes; evidence opens the browser's own viewer at the page (`#page=N`) until then |
| **R5** sparse arm + RRF | ✅ keyword arm with IDF ranking, RRF fusion (3 tests), versioned ingest; harness `tools/FinRiskLensAI.RagCli`; sample corpora in `Doc/knowledge/` ingested on Neon. "FOIR" and "44AB" hit their clauses at rank 1 |
| **R4** dense retrieval | ✅ 2026-10-12 — pgvector cosine through HNSW, filtered to the current embedding model; fused with the keyword arm by RRF |
| **R10** reranker | ✅ built early — Flash scores the fused top 20. Fixed the one query hybrid fusion got wrong, and scores off-topic questions 0.00 (the refusal signal R6 needs). See `11_RAG_ARCHITECTURE.md` §10 |

Decisions made while building:
- **C#, not SQL.** The checks run over one case's rows in memory rather than as hand-written
  SQL. A case is a few thousand rows at most, so there is nothing to gain from the database,
  and every check is a pure function with a unit test that needs no database. The SQL
  dialect notes in §6 no longer apply to the checks.
- **One finding per document per check,** with every offending row as evidence (capped at
  25). A statement with forty broken balances is one problem, not forty cards.
- **Metadata flags consumer editors, not unknown producers.** A denylist (Word, Photoshop,
  iLovePDF, Canva…) in `FraudOptions.ConsumerEditors`. An allowlist of bank generators
  would flag every salary slip and ITR, which are not bank-generated.
- **Verified end to end** on Neon with hand-made rows standing in for the extractor: all
  three planted problems found, a dismissal survived a re-run, an unsigned review post was
  refused.

### Phase 4 — Investigator + grounding · Oct 10 – Oct 12
| Lane | Work |
|---|---|
| Main | `Investigator` + tools (scoped server-side) · `GroundingVerifier` + `AiDropLog` · `CounterpartyClassifier` |
| RAG | **R6** `RagAnswerer` + citation schema + refusal · **R7** `RagGroundingVerifier` + `RagQueryLog` · **R8** `KnowledgeSearchTool` wired into the Investigator |

**Status 2026-10-12 — built and measured.**

| Item | Status |
|---|---|
| `Investigator` | ✅ `gemini-3.1-pro-preview` (falls back to `gemini-2.5-pro`), function calling, 8-turn cap — the last turn may only call `submit_findings`. Runs in the pipeline after the deterministic checks, and from **Run investigation** on the case page |
| Tools, scoped server-side | ✅ `get_case_overview` · `get_statement_rows` · `get_counterparty_summary` · `get_document_fields` · `get_verification_facts` (GST / ITR / AA for the linked Udyam number) · `search_policy` · `submit_findings`. Every tool is bound to one application's data loaded before the run |
| `GroundingVerifier` + `AiDropLog` | ✅ drops a claim whose type is not in the AI catalogue, that cites a row or field outside the case, or that quotes a rupee amount matching no cited value, cited total, per-counterparty total, declared figure or verification fact. Uncited policy is stripped. 9 tests. Rejected claims are shown on the case page, not hidden |
| **R8** knowledge tool | ✅ `search_policy` is the hybrid search with rerank; citations become `Policy` evidence showing the clause breadcrumb |
| `CounterpartyClassifier` | ✂ not needed: the counterparty summary plus the model's own reading classified lenders, betting and crypto correctly |
| **R6/R7** standalone answerer | ⏸ next — the Investigator already exercises retrieval, citation and verification |

**Measured** (`DemoData investigate`, all 12 demo cases, live):

| | |
|---|---|
| Expected AI findings found | **5/5** — undeclared EMIs and loan stacking (A01), gambling/crypto and a NACH bounce (A02), slip net pay vs salary actually credited (T03) |
| Raised on genuine cases | **0** — including a mutual-fund NACH the model correctly did not call an EMI |
| Repeats of deterministic findings | 0 |
| Time per case | 12–43 s |
| Claims dropped by grounding | 0 in the final run. The first run dropped one: a correct per-lender EMI total the verifier did not yet allow — fixed by accepting per-counterparty totals of cited rows, with a test |

Found while building: a single Vertex call can hang past any sensible deadline. Each
attempt now has a two-minute deadline and timeouts retry like a 503, so the Pro → 2.5 Pro
fallback can actually fire.

> **R8 is where the two lanes meet.** The agent stops saying "gambling exposure
> detected" and starts saying "7.2% of monthly credits went to betting merchants,
> exceeding your 5% threshold requiring senior credit approval [Credit Policy §4.2]".

### Phase 5 — Cross-checks + network lens · Oct 13 – Oct 15
| Lane | Work |
|---|---|
| Main | **`AaCrossCheck` first** (differentiator, reuses existing code) · `RegistryCrossCheck` (MCA employer) · layout embeddings · graph tables · union-find · ring narrative · Ring Explorer |
| RAG | **R9** case-memory write-back on finding confirmation |

### Phase 6 — Demo, video, deck, submit · Oct 15 – Oct 16
- Demo data generator run: 20 genuine · 4 tampered · 12 ring · 3 edge cases
- Knowledge corpus seeded (synthetic policy, typology, regulatory extracts)
- 3-minute video · deck PDF · **submit two days early**

**Status 2026-10-03 — generator built ahead of schedule** (`tools/FinRiskLensAI.DemoData`):

| Item | Status |
|---|---|
| Synthetic PDFs (QuestPDF) | ✅ multi-page statements and salary slips from two fictional banks and a fictional employer; every name contains "Demo" and every page carries a SYNTHETIC footer |
| Truth files | ✅ each document's `ExtractedDocument` as JSON — what an exact extractor reads off it. Drives the demo now; becomes the extractor's eval set once Gemini is connected |
| Cases | ✅ 10 so far: 4 genuine, 3 tampered (inflated receipt, spliced page, fabricated slip), 3 behavioural (round-tripping, window dressing, pass-through) |
| Expected vs actual | ✅ `DemoScenarioTests` (10 cases, in memory) and the `seed` command (real intake → truth applied as extraction → checks) both pass: genuine cases raise nothing, the others raise exactly their expected findings |
| Seeded on Neon | ✅ cases #4–#13 (`DEMO-*`); `seed` skips cases that already exist |
| Ring cases (12) · edge cases (3) | ⏸ ring cases need the network lens (Phase 5) to have anything to find; edge cases next — QuestPDF can encrypt, so a password-protected statement is cheap |

```bash
dotnet run --project tools/FinRiskLensAI.DemoData -- generate <dir>
```

```bash
dotnet run --project tools/FinRiskLensAI.DemoData -- seed
```

`seed` refuses to run unless `AzureBlob:Container` is the fork's own container — V1's
`msme-data` holds live data under IDBI evaluation.
- **R10** reranker + tuning against the eval set — only if time allows

---

## 8. Cut list

In order, if you fall behind:

1. **R10** reranker and query expansion — hybrid retrieval alone hits the recall target
2. **Ring Explorer graph UI** — a table of ring members demos nearly as well
3. **Ask-the-Case chat** — the Investigator already produces the findings
4. **Insights dashboard** — fold the two counters into the Cases Inbox header
5. **R9** case memory — valuable, but it needs volume to be impressive anyway

**Never cut:** the grounding verifiers (both of them) and the evidence viewer. They
are what make the pitch credible rather than another demo of a model being confident.

---

## 9. Team split

| Role | Owns |
|---|---|
| **AI engineer** | Extractor schemas and prompts, Investigator + tools, ring analyst |
| **RAG engineer** | R1–R10 end to end, knowledge corpora, eval set |
| **Data / SQL** | Case schema, deterministic checks, graph tables, grounding verifiers, tests |
| **Full-stack / cloud** | MVC screens, PDF.js viewer, Cytoscape, GCP, demo data, deck, video |

With three people, the RAG lane merges into the AI engineer's after R5 — by then the
retrieval layer is stable and the remaining work is integration.

---

## 10. Definition of done

- [ ] Cloud Run URL works in incognito, with sample documents available in-app
- [ ] A genuine case returns **no findings** — and says so
- [ ] A tampered case returns findings with **highlighted page regions**
- [ ] At least one finding cites a **policy clause** via RAG
- [ ] At least one finding comes from **AA or MCA cross-check**, not the upload alone
- [ ] The ring case shows **≥ 10 linked applications** and a generated narrative
- [ ] The dashboard shows discarded AI findings **and** stripped RAG claims
- [ ] Hybrid retrieval demonstrably beats dense-only on the eval set (both arms live)
- [ ] Every table in `DB_SCRIPTS.md`; every threshold in config
- [ ] Synthetic data only — no real bank names or logos anywhere
- [ ] Services stay up through the evaluation window (Oct 19 – Nov 6)
