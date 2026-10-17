# FinRiskLens — Architecture

*Evidence-based fraud detection and risk analysis for lending · Google AI Builder Cup 2026 (BFSI)*

How the system is put together. The build history and every measurement are in
[`10_FRAUD_LENS_BUILD.md`](10_FRAUD_LENS_BUILD.md); retrieval in depth is in
[`11_RAG_ARCHITECTURE.md`](11_RAG_ARCHITECTURE.md); every table change is in
[`DB_SCRIPTS.md`](DB_SCRIPTS.md).

---

## 1. What it does

> **One assessment per customer** ([`12_CUSTOMER_RISK_LENS.md`](12_CUSTOMER_RISK_LENS.md), Phase 7).
> The bank works per customer only: there are no case pages and no Rings page — links between
> customers show in each customer's Risk Lens. ✅ marks what runs today; 🆕 what Phase 7 still adds.

```
 Customer (MSME)                         Bank officer
 ① Register with Udyam ✅                ③ Customers — risk status per customer ✅
 ② Share data ──────────────────────►   ④ Risk Lens — verdict + a section per source ✅
    fetch: AA ✅ · GST ✅ · ITR ✅ · MCA ✅  ⑤ Confirm / dismiss findings ✅
    upload documents ✅                  ⑥ Clear? → loan case + AI credit decision ✅ (capacity: T6 🆕)
                                         ⑦ Send to LOS · ULI (OCEN) · ONDC ✅ (+ decision blocks 🆕)
```

The customer shares what lenders ask for — bank statements through Account Aggregator, GST,
ITR and company records, and any documents they upload. FinRiskLens returns **findings** on that
customer: each names what is wrong, explains it in rupees, and links to the exact row, field or
page that shows it. The officer confirms or dismisses each one; a customer with no open risk
goes on to a loan case with an AI credit decision, sent to the bank's LOS, ULI or ONDC.

*The bank never opens a case: the customer shares and uploads, and everything joins that customer's
one assessment, which §2's engine reassesses whenever a source arrives.*

Three kinds of evidence, in this order:

| Layer | Asks | Finds |
|---|---|---|
| **The document itself** | Does its own arithmetic and file history hold? | Edited balances, spliced pages, typed-in net pay, files made in Word or Canva |
| **The source of truth** | Does it match what the bank or registry holds? | Statements that differ from the bank's Account Aggregator record |
| **The network** | Does it share what unrelated customers never share? | A director, mobile, email, address, document or salary account shared across customers, or GST invoices that go round in a loop — the linked customers, named on each one's Risk Lens |
| **Judgement** | What does a careful investigator see across all of it? | Undeclared EMIs, loan stacking, gambling exposure, slip vs salary credited — each cited to rows and to the lender's policy |

## 2. The assessment pipeline

The engine runs on the customer's one assessment, whenever they upload a document or a source
(Account Aggregator, GST, ITR, MCA) arrives.

```
Share / upload ──► ① INGEST ──► ② EXTRACT ──► ③ CHECK ──► ④ LINK ──► ⑤ INVESTIGATE ──► ⑥ VERIFY ──► ⑦ RISK LENS ──► ⑧ DECIDE
```

| Step | Where | What happens |
|---|---|---|
| ① Ingest | `Fraud/Cases/CaseService`, `DocumentInspector` | Type sniffed from the bytes (PDF, PNG, JPEG only), SHA-256, PDF producer, creator, dates and fonts. Stored content-addressed in Cloud Storage, Mumbai |
| ② Extract | `Fraud/Ai/DocumentExtractor` | Gemini Flash in Mumbai reads every page in parallel into a fixed schema; numbers copied as printed, never corrected. A page whose own arithmetic does not add up is read once more with more thinking — a tampered page fails both reads and is kept as printed. The statement's account number is read in a call of its own |
| ③ Check | `Fraud/Checks/DeterministicChecks`, `SourceChecks` | Nine pure C# checks: running balance, page carry-over, stated totals, slip arithmetic, round-tripping, window dressing, pass-through, PDF metadata, and the statement against the bank's own Account Aggregator record; the Account Aggregator risk flags become findings |
| ④ Link | `Fraud/Checks/NetworkLens` | Across customers: same file, same template claiming different issuers, same salary account behind different employers, same director, mobile, email or address, circular GST trading — union-find groups linked customers, and each gets a finding naming the others |
| ⑤ Investigate | `Fraud/Ai/Investigator`, `InvestigatorTools` | Gemini Pro agent, function calling, 8-turn cap. Its tools are bound server-side to one customer's assessment: overview, statement rows, counterparty summary, document fields, verification facts, policy search |
| ⑥ Verify | `Fraud/Ai/GroundingVerifier` | An AI claim survives only if every cited row or field belongs to this customer's assessment and every rupee amount traces to them. Rejected claims are logged (`ai_drop_log`) and shown on the Risk Lens |
| ⑦ Risk Lens | `BankAdminController.Customer`, `Views/Shared/_RiskLensBody`, `_FindingCard`, `RiskLensController` | The verdict (`RiskSummary.Of`) and one section per source; findings most severe first, evidence opening the document at its page, policy clauses by breadcrumb; confirm / dismiss / reopen with a note. A confirmed finding blocks the loan case |
| ⑧ Decide | `Fraud/Credit/CreditDecisionService`, `CreditCapacity` | Clear customers only: capacity computed in code, Gemini Pro decides within it, every figure verified; the accepted decision gates the push to LOS / ULI / ONDC |

`Fraud/Pipeline/CasePipeline` runs ②–⑥ off the request thread (`Channel<T>` + a hosted
worker) and re-queues anything a restart interrupted.

## 3. Solution structure

| Project | Role |
|---|---|
| `FinRiskLensAI-RAG-V2` | ASP.NET Core 8 MVC web app: bank portal (Customers, Customer 360 with the Risk Lens, loan-case push), MSME onboarding, consent and upload journeys, the source-data ingest API |
| `FinRiskLensAI.Fraud` | The product: extraction, checks, network lens, investigator, grounding, RAG, Cloud Storage stores, pipeline |
| `FinRiskLensAI.Core` | Entities, view models, interfaces — no infrastructure |
| `FinRiskLensAI.Data` | EF Core 8 + Npgsql, repositories, migrations (PostgreSQL + pgvector) |
| `FinRiskLensAI.Services` | Verification-source connectors and analyses: Udyam, GST, ITR, Account Aggregator (Finvu), MCA; loan-case push to LOS / ULI / ONDC |
| `FinRiskLensAI.ML` | Source-facts extraction from the stored registry and bank payloads (what the investigator's facts tool reads) |
| `tools/FinRiskLensAI.DemoData` | The synthetic test set with ground truth: `generate`, `seed`, `eval` (live extraction scored row by row, the network lens on Gemini's reading), `investigate` |
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

Snake-case naming, int identity keys, audit columns stamped on save. Links between customers are
computed from these tables on demand — no link table (see `CaseRepository.GetNetworkAsync` for the
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

In the demo, Udyam, GST, ITR and MCA payloads are generated samples in the documented API shapes
(the GST analysis page shows one fixed sample dataset for every business); Account Aggregator runs
on the live Finvu sandbox. The MCA page's OFAC and AML board screening is a demo
(`McaController.ScreenDirector`): the director's name against a short fictional list, labelled as
such on every result — a licensed screening provider replaces it to go live.

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
