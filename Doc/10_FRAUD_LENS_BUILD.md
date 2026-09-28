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
ML/Features/                    Gst | Itr | Udyam FeatureExtractor   (score features)
Controllers/                    Scoring, ScoreHistory, Report
Views/                          Dashboard/FinancialHealthCard, Report/
wwwroot/                        score-gauge.js, score-history.js
Data/Configurations/            MsmeScoreSummary, MsmeScoreHistory
Core/Models/Scoring/
```

**Packages dropped:** `Microsoft.ML`, `Microsoft.ML.LightGbm`, `Microsoft.ML.TimeSeries`,
`Azure.Storage.Blobs`, `Npgsql.EntityFrameworkCore.PostgreSQL` (unused legacy).

**Kept but repurposed:** `AaFeatureExtractor` — it parses AA data, which the AA
cross-check needs. Moves into the fraud project rather than being deleted.

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
`Applications`, `Documents`, `StatementRows`, `PageSummaries`, `DocFields`,
`Findings`, `Evidence`, `Rings`, `AiDropLog`, graph tables (`gApplication`,
`gDocument`, `gEntity`, `gTemplate` + edges), and the RAG tables
(`KnowledgeDocs`, `KnowledgeChunks`, `ChunkEmbeddings`, `RagQueryLog`).

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
| Cloud SQL for SQL Server | `sqladmin.googleapis.com` | Main DB — **graph tables + full-text required** |
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
- **Cloud SQL for SQL Server has no free tier** — roughly $50–90/month at the
  smallest usable size. **Stop the instance when not working.** The $300 trial
  credit covers the project comfortably if you do.
- **Gemini may not serve from `asia-south1`.** Documents stay in Mumbai; AI calls
  may route via `global`. Fine for synthetic data — say so in the deck rather than
  letting a judge find it.

### Local development
`gcloud auth application-default login` — no key files in the repo, ever.

---

## 6. Project updates

**Packages added:** `Google.Cloud.AIPlatform.V1` (or `Google.GenAI`) ·
`Google.Cloud.Storage.V1` · `UglyToad.PdfPig` · `Dapper` · `QuestPDF` (tools only) ·
`xunit` + `Microsoft.NET.Test.Sdk`

**`IMsmeDataStore` must change.** It is string-only today:
```csharp
Task UploadAsync(string uan, string fileName, string content, ...);
Task<string?> DownloadAsync(...);
```
PDFs are binary. Rename to `ICaseDocumentStore`, add byte overloads and
`Task<string> GetSignedUrlAsync(...)` for the PDF viewer.

**`appsettings.json`:** remove `AzureBlob`; add `Gcp`, `Gcs`, `Gemini`, `Fraud`,
`Rag` (see [`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md) §16). Keep `Finvu`,
`Database`, `Smtp`, `Jwt`, `Serilog`. `Groq` goes once Gemini replaces the chatbot.

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

### Phase 1 — Prove the platform on GCP · Sep 30 – Oct 2
| Lane | Work |
|---|---|
| Main | `GcsDocumentStore`, `GeminiClient`, Cloud SQL provisioned, existing migrations applied, **existing app deployed to Cloud Run unchanged** |
| RAG | **R1** — knowledge schema + migration, logged in `DB_SCRIPTS.md` |

> Ship nothing new until the current app runs on Google's infrastructure. Three days
> that de-risk everything after them.

### Phase 2 — Case ingest + extraction · Oct 3 – Oct 6
| Lane | Work |
|---|---|
| Main | Case schema + migration · upload screen · PdfPig metadata · `DocumentExtractor` (Flash, boxes) · `CasePipelineWorker` |
| RAG | **R2** chunkers + unit tests · **R3** `EmbeddingClient` + ingest the typology corpus (smallest, cleanest) |

### Phase 3 — Deterministic findings + Case Report · Oct 7 – Oct 9
| Lane | Work |
|---|---|
| Main | Seven SQL checks + tests · Case Report UI · PDF.js evidence highlighting |
| RAG | **R4** `SqlVectorIndex` + dense retrieval + CLI harness · **R5** sparse arm + RRF fusion |

> **End of Phase 3 is the insurance line:** a demoable product with zero AI risk.
> If everything after this fails, there is still something to show.

### Phase 4 — Investigator + grounding · Oct 10 – Oct 12
| Lane | Work |
|---|---|
| Main | `Investigator` + tools (scoped server-side) · `GroundingVerifier` + `AiDropLog` · `CounterpartyClassifier` |
| RAG | **R6** `RagAnswerer` + citation schema + refusal · **R7** `RagGroundingVerifier` + `RagQueryLog` · **R8** `KnowledgeSearchTool` wired into the Investigator |

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
- [ ] Every table in `DB_SCRIPTS.md`; every threshold in config
- [ ] Synthetic data only — no real bank names or logos anywhere
- [ ] Services stay up through the evaluation window (Oct 19 – Nov 6)
