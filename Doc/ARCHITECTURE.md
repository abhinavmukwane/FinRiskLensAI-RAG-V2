# FinRiskLens — Architecture

*Evidence-based fraud detection and risk analysis for lending · Google AI Builder Cup 2026 (BFSI)*

How the system is put together. The build history and every measurement are in
[`10_FRAUD_LENS_BUILD.md`](10_FRAUD_LENS_BUILD.md); retrieval in depth is in
[`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md); every table change is in
[`DB_SCRIPTS.md`](DB_SCRIPTS.md).

---

## 1. What it does

A credit officer opens a case and uploads what the applicant submitted — bank statements,
salary slips, ITRs, as PDF or image. FinRiskLens returns **findings**: each names what is
wrong, explains it in rupees, and links to the exact row, field or page that shows it. The
officer confirms or dismisses each one.

Three kinds of evidence, in this order:

| Layer | Asks | Finds |
|---|---|---|
| **The document itself** | Does its own arithmetic and file history hold? | Edited balances, spliced pages, typed-in net pay, files made in Word or Canva |
| **The source of truth** | Does it match what the bank or registry holds? | Statements that differ from the bank's Account Aggregator record |
| **The network** | Does it share what unrelated applicants never share? | One template, one file or one salary account across separate applications — a fraud ring |
| **Judgement** | What does a careful investigator see across all of it? | Undeclared EMIs, loan stacking, gambling exposure, slip vs salary credited — each cited to rows and to the lender's policy |

## 2. The case pipeline

```
Upload ──► ① INGEST ──► ② EXTRACT ──► ③ CHECK ──► ④ LINK ──► ⑤ INVESTIGATE ──► ⑥ VERIFY ──► ⑦ REVIEW
```

| Step | Where | What happens |
|---|---|---|
| ① Ingest | `Fraud/Cases/CaseService`, `DocumentInspector` | Type sniffed from the bytes (PDF, PNG, JPEG only), SHA-256, PDF producer, creator, dates and fonts. Stored content-addressed in Cloud Storage, Mumbai |
| ② Extract | `Fraud/Ai/DocumentExtractor` | Gemini Flash in Mumbai reads every page in parallel into a fixed schema; numbers copied as printed, never corrected. A page whose own arithmetic does not add up is read once more with more thinking — a tampered page fails both reads and is kept as printed. The statement's account number is read in a call of its own |
| ③ Check | `Fraud/Checks/DeterministicChecks` | Nine pure C# checks: running balance, page carry-over, stated totals, slip arithmetic, round-tripping, window dressing, pass-through, PDF metadata, and the statement against the bank's own Account Aggregator record |
| ④ Link | `Fraud/Checks/NetworkLens` | Same file, same template claiming different issuers, same salary account behind different employers — union-find joins applications into rings |
| ⑤ Investigate | `Fraud/Ai/Investigator`, `InvestigatorTools` | Gemini Pro agent, function calling, 8-turn cap. Its tools are bound server-side to one application: overview, statement rows, counterparty summary, document fields, verification facts, policy search |
| ⑥ Verify | `Fraud/Ai/GroundingVerifier` | An AI claim survives only if every cited row or field belongs to the case and every rupee amount traces to them. Rejected claims are logged (`ai_drop_log`) and shown on the case page |
| ⑦ Review | `CasesController`, `Views/Cases/Detail` | Findings most severe first, evidence opening the document at its page, policy clauses by breadcrumb; confirm / dismiss / reopen with a note |

`Fraud/Pipeline/CasePipeline` runs ②–⑥ off the request thread (`Channel<T>` + a hosted
worker) and re-queues anything a restart interrupted.

## 3. Solution structure

| Project | Role |
|---|---|
| `FinRiskLensAI-RAG-V2` | ASP.NET Core 8 MVC web app: bank portal (Cases, Rings, Customer 360), MSME onboarding and consent journeys, the source-data ingest API |
| `FinRiskLensAI.Fraud` | The product: extraction, checks, network lens, investigator, grounding, RAG, Cloud Storage stores, pipeline |
| `FinRiskLensAI.Core` | Entities, view models, interfaces — no infrastructure |
| `FinRiskLensAI.Data` | EF Core 8 + Npgsql, repositories, migrations (PostgreSQL + pgvector) |
| `FinRiskLensAI.Services` | Verification-source connectors and analyses: Udyam, GST, ITR, Account Aggregator (Finvu), MCA; loan-case push to LOS / ULI / ONDC |
| `FinRiskLensAI.ML` | Source-facts extraction from the stored registry and bank payloads (what the investigator's facts tool reads) |
| `tools/FinRiskLensAI.DemoData` | Synthetic cases with ground truth: `generate`, `seed`, `eval` (live extraction scored row by row, rings on Gemini's reading), `investigate` |
| `tools/FinRiskLensAI.RagCli` | Build and query the knowledge base without the UI |
| `tests/FinRiskLensAI.Tests` | xUnit: every check, the network lens, grounding, chunking, fusion, PDF inspection, every demo case |

Dependency injection is Autofac with convention scanning (`*Service`, `*Repository`);
`FraudModule` registers the Gemini client, extractor, investigator, queue and the two
Cloud Storage stores, and fails on first use if a bucket is not configured.

## 4. Google Cloud

| Service | Use | Region |
|---|---|---|
| **Vertex AI — Gemini Flash** | Document extraction | `asia-south1` (Mumbai) |
| **Vertex AI — Gemini embeddings** | 768-dimension vectors for the knowledge base | `asia-south1` |
| **Vertex AI — Gemini Pro** | Investigator (falls back to a second Pro model) | `global` — sees extracted facts only |
| **Vertex AI — Gemini Flash** | Reranker for policy search | `global` |
| **Cloud Run** | The web app, built from source by buildpacks; one warm instance with session affinity | `asia-south1` |
| **Cloud SQL for PostgreSQL 16 + pgvector** | Cases, findings, evidence, knowledge chunks and vectors | `asia-south1` |
| **Cloud Storage** | `finrisklensai-cases` (uploads) · `finrisklensai-sources` (registry and AA payloads) · `finrisklensai-keys` (sign-in keys, from the next deploy) | `asia-south1` |

Applicant documents are stored, read and embedded in Mumbai. Model names and locations are
configuration (`Gemini:*`), never code. Credentials are Application Default: the developer's
gcloud login locally, the `finrisk-run` service account on Cloud Run. The deploy command and
every flag are in `10_FRAUD_LENS_BUILD.md` → *Deploying*.

## 5. Data

| Table | Holds |
|---|---|
| `applications` | One case: applicant reference, product, Udyam number, declared income, EMIs, employer, status |
| `documents` | Each upload: hash, storage path, doc type, PDF metadata, claimed issuer, extraction status |
| `statement_rows` · `page_summaries` · `doc_fields` | What extraction read — rows with counterparties, page balances and totals, named fields |
| `findings` · `evidence` | Findings with category, severity, source (deterministic or AI-verified), review state; evidence pointing at rows, fields, pages, documents or policy chunks |
| `ai_drop_log` | AI claims the grounding verifier rejected, per investigation run |
| `knowledge_docs` · `knowledge_chunks` · `chunk_embeddings` · `rag_query_log` | The RAG corpora: credit policy, fraud typology, regulation |

Snake-case naming, int identity keys, audit columns stamped on save. Rings are computed
from these tables on demand — no ring table (see `CaseRepository.GetNetworkAsync` for the
ceiling and the upgrade path).

## 6. Verification sources

The truth uploads are checked against, stored per MSME as JSON in
`gs://finrisklensai-sources/{UDYAM-number}/`:

| Source | Collected by | Used for |
|---|---|---|
| **Account Aggregator** (Finvu) | MSME consent journey → FI fetch | Check 9 (statement vs bank record); AA facts for the investigator |
| **GST** returns | GST connector | Turnover and filing facts |
| **ITR** | ITR connector | Filed turnover and income |
| **Udyam**, **MCA** | Registry connectors | Identity, company status, directors |

Payloads arrive through the ingest API, `PUT /api/msme-data/{uan}/manifest`,
`PUT /api/msme-data/{uan}/files/{fileName}`, `GET /api/msme-data/{uan}/status` — closed
unless the caller sends `X-Ingest-Key`.

## 7. Security

- Bank portal behind its own sign-in (`BankAdminAuthorize`); MSME side by email OTP.
- Session cookies signed with Data Protection keys kept in a private bucket only the
  service account can read.
- Production secrets live in a git-ignored `appsettings.Production.json`, uploaded with the
  source at deploy; the repository is public and holds none.
- Cloud SQL accepts encrypted connections only; Cloud Run reaches it through the connector
  socket, with no network path opened.
- The investigator cannot reach outside its case: every tool is bound server-side to one
  application's data before the run.
