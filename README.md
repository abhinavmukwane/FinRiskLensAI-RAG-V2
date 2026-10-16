# FinRiskLens — evidence-based fraud detection for lending

**Every finding backed by evidence.** FinRiskLens reads the documents submitted with a loan
application — bank statements, salary slips, ITRs — and returns fraud findings that each
name what is wrong and point at the exact row, field or page that shows it.

A tampered statement whose running balance stops adding up, a careful forgery that
disagrees with the bank's own record, a salary slip whose net pay was typed in, money that goes straight back to its payer, EMIs to lenders the applicant
never declared — found, explained in rupees, and linked to the evidence, before a credit
officer relies on the documents.

Built on Google Cloud with **Gemini on Vertex AI** for the **Google AI Builder Cup 2026
(BFSI track)** by team **ASM FinTech Developer**. It grows out of
[FinRiskLensAI](https://github.com/abhinavmukwane/FinRiskLensAI), our MSME credit platform
shortlisted in the top 5 at IDBI Innovate 2026, and reuses its verification-source
integrations.

---

## Links

| | |
|---|---|
| **Live application** | <https://finrisklensai.cloud.run> |
| **Build plan and status** | [`Doc/10_FRAUD_LENS_BUILD.md`](Doc/10_FRAUD_LENS_BUILD.md) |
| **RAG architecture** | [`Doc/11_RAG_ARCHITECTURE.md`](Doc/11_RAG_ARCHITECTURE.md) |
| **Database changes** | [`Doc/DB_SCRIPTS.md`](Doc/DB_SCRIPTS.md) |
| **Next: one assessment per customer** | [`Doc/12_CUSTOMER_RISK_LENS.md`](Doc/12_CUSTOMER_RISK_LENS.md) · team tasks in [`Doc/13_TEAM_WORK_PLAN.md`](Doc/13_TEAM_WORK_PLAN.md) |

---

## Try it

The live site runs on **synthetic demo cases only** — every bank and employer in them is
fictional and every page is stamped *SYNTHETIC DOCUMENT*.

1. Sign in to the bank portal at <https://finrisklensai.cloud.run/Auth/BankLogin>.
   Credentials are shared with the evaluation panel directly rather than published here.
2. Open **Cases**. Each demo case shows its findings, the evidence behind them, and the
   documents themselves. **Rings** lists the applications linked to each other.
3. Upload your own PDF statement or slip with **New case** and watch it go through the
   pipeline, or press **Run investigation** on any case.

| Case | What it is | What FinRiskLens finds |
|---|---|---|
| DEMO-G01 … G05 | Five genuine applicants — four businesses, one salaried. G05 has Account Aggregator linked, and some of its bank postings clear a day after the statement date | Nothing. False alarms on honest applicants are the costliest error |
| DEMO-T01 | A receipt inflated tenfold in Word; balances and footer left alone | Balance break, page totals mismatch, file edited in Word |
| DEMO-T02 | Page 3 swapped in from a healthier quarter | Page carry-over break, balance break, file merged in iLovePDF |
| DEMO-T03 | Salary slip with net pay typed higher, made in Canva | Slip arithmetic error, file history — and the AI notices the slip disagrees with the salary actually credited |
| DEMO-T04 | A careful forgery: two receipts tripled and four loan EMIs deleted, then every balance and page total recomputed and the bank's own PDF producer kept | Every arithmetic and metadata check passes. The bank's own record, fetched through Account Aggregator, does not: two amounts changed, four EMIs missing, ₹3.66 lakh more in credits and ₹1.30 lakh less in debits than the bank recorded |
| DEMO-B01 | Large receipts sent back to the payer within two days | Round-tripping |
| DEMO-B02 | ₹25 lakh parked for six days just before applying | Window dressing |
| DEMO-B03 | Every receipt forwarded the same day | Pass-through (mule-like account) |
| DEMO-A01 | Monthly NACH EMIs to two lenders, never declared; two fresh loans 48 days apart | Undeclared EMIs, loan stacking — cited to the credit policy |
| DEMO-A02 | Weekly betting, monthly crypto, a bounced EMI | Gambling and crypto exposure — cited to policy §4.2 — and the bounce |
| DEMO-R01 … R04 | Four salaried applicants supplied by one fabricator. Each case is clean on its own | A fraud ring: statements claiming two banks and slips claiming three employers, all made by the same software — and every salary paid from one account |
| DEMO-R05, R06 | Two businesses; the second uploaded the first one's statement | A fraud ring: the same file, byte for byte |

---

## How it works

```
 Upload (PDF / image)
   │
   ① INGEST       type sniffed from the bytes · SHA-256 · PDF metadata (producer, dates, fonts)
   │              stored in Cloud Storage, Mumbai — the same file in two cases is linked at once
   ▼
   ② EXTRACT      Gemini 3.5 Flash, Mumbai — page by page in parallel; numbers copied as printed,
   │              never corrected, because tampered arithmetic is the evidence
   ▼
   ③ CHECK        nine deterministic checks in C#: balance continuity, page carry-over, stated
   │              totals, slip arithmetic, round-tripping, window dressing, pass-through, metadata —
   │              and the statement against the bank's own record, fetched through Account Aggregator
   ▼
   ④ LINK         the network lens across every application: a shared file, a template claiming
   │              different issuers, or one salary account behind different employers joins
   │              applications into a fraud ring (union-find), and every member gets the finding
   ▼
   ⑤ INVESTIGATE  Gemini 3.1 Pro agent with tools scoped to this one application: statement rows,
   │              counterparty summary, document fields, GST / ITR / Account Aggregator facts,
   │              and a hybrid search over the lender's policy and fraud typologies
   ▼
   ⑥ VERIFY       every AI claim must cite rows or fields of this case, and every rupee amount
   │              must trace to them — or it is dropped, and the drop is shown, not hidden
   ▼
   ⑦ REVIEW       findings with evidence links and policy citations; confirm, dismiss, reopen
```

### Three kinds of evidence

| | Role |
|---|---|
| **Uploaded documents** — statements, slips, ITRs, KYC | What is under investigation |
| **Registry and Account Aggregator data** — Udyam, GST, ITR, MCA, bank data via AA | What the uploads are checked against |
| **Knowledge corpora** — credit policy, fraud typologies | Judgement context the AI cites |

Most document-fraud tools only have the first. The second is what lets FinRiskLens compare
a business's claimed turnover with its GST returns and filed ITR; the third is what lets a
finding end with *"exceeds the 5% limit in Credit Policy §4.2"* instead of an opinion.

---

## Measured, not assumed

Every number below comes from a command in this repository, run against live Gemini on the
demo cases. The demo generator writes each document's ground truth alongside it, so
extraction is scored row by row.

**Extraction** — `DemoData eval`, 17 documents, 1,298 statement rows, two consecutive runs
(2026-10-14)

| | |
|---|---|
| Rows exact (date, debit, credit and balance all right) | **99.8% and 100%** (1,296 and 1,298 of 1,298) |
| Page balances and printed totals | **100% (244/244)** |
| Fields — salary slips and statement account numbers | **100% (38/38)** |
| Cases whose findings match expectation | **14/14**, both runs |
| Time per statement | 10–60 s |

Reading a whole statement in one call first scored 88.8%: Gemini dropped 45 rows from a
*genuine* statement, which broke its arithmetic and produced false tamper findings on an
honest applicant. Splitting into pages read in parallel fixed it, and scored 895/895.

Two days later the same prompt scored 95.6%. Gemini had started reading `09-08-2026` as
8 September, giving up on a page partway through, and dropping a digit from a page total —
three false findings on honest cases. We only knew because the eval runs against ground
truth. Two fixes brought it back: the extractor is told that Indian documents print the day
first, and a page whose own arithmetic does not add up is read a second time with more
thinking. A tampered page fails both reads and is kept exactly as printed; the second read
is never told what failed, so the model is never invited to make the numbers add up.

**Network lens** — `DemoData eval` also runs the lens on Gemini's own reading. Over the nine
salaried and ring cases it finds exactly the two planted rings, each with every link, and
leaves the three genuine colleagues of one employer unlinked.

**Investigator** — `DemoData investigate`, the first 12 cases (the two added since raise nothing either)

| | |
|---|---|
| Expected AI findings found | **5/5** |
| Findings raised on genuine applicants | **0** |
| Repeats of what the deterministic checks already found | 0 |
| Time per case | 12–43 s |

**Retrieval** — hybrid search (keyword + vector + Gemini rerank) over the policy and
typology corpora: every test question, including paraphrases with no shared words, returns
the right clause first with relevance 1.00; an off-topic control question scores 0.00 on
everything.

---

## Responsible AI

- **Arithmetic is deterministic.** Balance, totals and slip checks are plain C# with unit
  tests. The model is only asked what arithmetic cannot answer.
- **Grounding verification.** An AI finding is dropped if its type is outside the AI
  catalogue, if it cites a row or field that is not in this application, or if it quotes a
  rupee amount that is not a cited value, a total of cited rows, a declared figure or a
  verification fact it was shown. Uncited policy is stripped. Rejected claims are listed
  on the case page with the reason.
- **Tools are scoped server-side.** The Investigator's tools are bound to one
  application's data before the run; the model cannot ask its way into another case.
- **A human decides.** Every finding is confirmed or dismissed by an officer, with a note.
  A re-run never resurrects a dismissed finding.
- **Data location.** Document files, the database, extraction and embeddings are all in
  Mumbai (`asia-south1`). Only Pro reasoning over already-extracted facts uses Gemini's
  global endpoint, because Pro is not served from Mumbai.
- **Synthetic data only.** No real applicant's document is in the demo.

---

## Google Cloud

| Service | Used for |
|---|---|
| **Vertex AI — Gemini 3.5 Flash** (`asia-south1`) | Reading statements and slips, page by page |
| **Vertex AI — Gemini 3.1 Pro** (global; falls back to 2.5 Pro) | The Investigator agent, function calling |
| **Vertex AI — Gemini 3.8 Flash** (global) | Reranking retrieved policy passages |
| **Vertex AI — gemini-embedding-001** (`asia-south1`) | 768-dimension embeddings for policy and typology |
| **Cloud Run** (`asia-south1`) | The application, built from source by Google Cloud buildpacks |
| **Cloud Storage** (`asia-south1`) | Uploaded case documents; registry and Account Aggregator source data |
| **Cloud SQL for PostgreSQL** (`asia-south1`) | Application data, findings, and the knowledge base — `pgvector` HNSW and full-text search in one database |
| **Cloud Build · Artifact Registry** | Source-to-container builds |
| **Cloud Logging** | Application logs |

Model names are configuration (`appsettings.json` → `Gemini`), never code.

---

## Architecture

```
FinRiskLensAI-RAG-V2     →  web app — controllers, Razor views, Program.cs, config
FinRiskLensAI.Fraud      →  Gemini client, extractor, Investigator, grounding verifier,
                            deterministic checks, RAG (chunker, hybrid search, rerank),
                            case pipeline, Cloud Storage stores
FinRiskLensAI.Services   →  application logic              (*Service,    auto-registered)
FinRiskLensAI.ML         →  verification-source parsers and the facts they produce
FinRiskLensAI.Data       →  EF Core 8, PostgreSQL + pgvector (*Repository, auto-registered)
FinRiskLensAI.Core       →  domain models and interfaces — no infrastructure references
tools/                   →  DemoData (synthetic documents, evals), RagCli (knowledge base)
tests/                   →  xUnit
```

Dependencies point inward toward `Core`. Retrieval is two SQL arms over one PostgreSQL
database — `tsvector` full-text ranked by term rarity, and `pgvector` HNSW cosine search —
fused with reciprocal rank fusion, then reranked by Gemini.

## Tech stack

ASP.NET Core 8 MVC (C#) · Autofac · EF Core 8 · PostgreSQL with `pgvector` · Vertex AI
over REST with Application Default Credentials · Google.Cloud.Storage.V1 · PdfPig (PDF
metadata and page splitting) · QuestPDF (synthetic documents, tools only) · Razor,
Bootstrap 5, Chart.js · xUnit.

---

## What is real, and what is synthetic

| | Status |
|---|---|
| Upload, extraction, checks, Investigator, grounding, review | **Working end to end** on Google Cloud |
| Demo documents | **Synthetic**, generated by `tools/FinRiskLensAI.DemoData` with their ground truth |
| Credit policy and fraud typologies | **Samples written for the demo** — not any lender's policy ([`Doc/knowledge/`](Doc/knowledge/)) |
| Udyam / GST / ITR / MCA responses | Cached and synthetic, in the documented API shapes |
| Account Aggregator | Consent journey against the Finvu sandbox |
| LOS / ULI / ONDC push | Payloads built and recorded as `Simulated`; setting an endpoint makes it a real POST |
| Database | **Cloud SQL for PostgreSQL 16** with `pgvector`, Mumbai, in production; Neon for development |

---

## Running locally

Requires the **.NET 8 SDK**, a **PostgreSQL** database with the `pgvector` extension (Neon
works), and the **gcloud CLI** signed in to a project with Vertex AI and Cloud Storage
enabled.

```bash
git clone https://github.com/abhinavmukwane/FinRiskLensAI-RAG-V2.git
```

```bash
gcloud auth application-default login
```

`FinRiskLensAI-RAG-V2/appsettings.json` holds the non-secret settings: Gemini models,
Cloud Storage buckets, retrieval knobs. Point `Gcp:ProjectId` and the `Gcs` buckets at your
own project, then create `FinRiskLensAI-RAG-V2/appsettings.Development.json` (git-ignored)
with your secrets — at least `Database:ConnectionStrings:PostgreSQL`.

```bash
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
```

```bash
dotnet run --project FinRiskLensAI-RAG-V2
```

Then seed the demo cases and the knowledge base:

```bash
dotnet run --project tools/FinRiskLensAI.DemoData -- seed
```

```bash
dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Typology Doc/knowledge/typology.md --title "Fraud Typology Catalogue" --version v1
```

```bash
dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Policy Doc/knowledge/credit-policy.md --title "Sample Credit Policy" --version v3
```

Deploying to Cloud Run is one command, with every setting explained, in
[`Doc/10_FRAUD_LENS_BUILD.md`](Doc/10_FRAUD_LENS_BUILD.md) under *Deploying*. Production
secrets go in `FinRiskLensAI-RAG-V2/appsettings.Production.json` — git-ignored like the
development file, and uploaded with the source when you deploy.

### Tools

| Command | Does |
|---|---|
| `DemoData -- generate <dir>` | Writes the synthetic PDFs and their ground-truth JSON |
| `DemoData -- seed` | Puts every demo case through real intake, extraction and the checks |
| `DemoData -- eval` | Live Gemini extraction scored row by row against the ground truth |
| `DemoData -- investigate` | Live Investigator on every demo case, scored against expected findings |
| `RagCli -- ingest · search · embed` | Builds and queries the knowledge base without the UI |

### Tests

63 xUnit tests: the nine deterministic checks (one hand-made statement each, plus a clean
one that must raise nothing), the network lens finding exactly the planted rings, the extractor's re-read rule, the grounding verifier, the chunker, reciprocal rank fusion,
PDF inspection, and every demo case run through the checks in memory.

```bash
dotnet test tests/FinRiskLensAI.Tests
```

---

## Documentation

| Document | Covers |
|---|---|
| [`12_CUSTOMER_RISK_LENS.md`](Doc/12_CUSTOMER_RISK_LENS.md) | The plan in force: customer and bank journeys, Risk Lens sections, AI credit decision, loan case to LOS / ULI / ONDC |
| [`ARCHITECTURE.md`](Doc/ARCHITECTURE.md) | How the system is put together — pipeline, projects, Google Cloud, data, verification sources, security |
| [`10_FRAUD_LENS_BUILD.md`](Doc/10_FRAUD_LENS_BUILD.md) | The build: what was kept, removed and added; phases, status, measurements, deployment |
| [`11_RAG_ARCHITECTURE.md`](Doc/11_RAG_ARCHITECTURE.md) | Retrieval in depth — corpora, chunking, embeddings, hybrid search, rerank, grounding |
| [`DB_SCRIPTS.md`](Doc/DB_SCRIPTS.md) | Every table created or altered, with the SQL |
| [`VIDEO_SCRIPT.md`](Doc/VIDEO_SCRIPT.md) | The 3-minute demo video — screens and voiceover |
| [`FinRiskLens_AI_Builder_Cup_2026.pptx`](Doc/FinRiskLens_AI_Builder_Cup_2026.pptx) | The submission deck, with a PDF alongside |
| [`knowledge/README.md`](Doc/knowledge/README.md) | The sample policy and typology corpora, and how to ingest them |

The documents of the IDBI Innovate version this grew out of were removed on 2026-10-15;
they remain in the git history.

## Repository layout

```
FinRiskLensAI-RAG-V2/     web application — controllers, views, wwwroot, appsettings
FinRiskLensAI.Fraud/      extraction, checks, Investigator, grounding, RAG, pipeline, storage
FinRiskLensAI.Services/   application logic
FinRiskLensAI.ML/         verification-source parsers
FinRiskLensAI.Data/       EF Core, repositories, PostgreSQL migrations
FinRiskLensAI.Core/       domain model and interfaces
tools/                    DemoData and RagCli
tests/                    xUnit tests
Doc/                      documentation, knowledge corpora, schema log
```

---

## Team

**ASM FinTech Developer**

## Status and licence

A hackathon build, shared publicly for evaluation. Findings are evidence for a human
reviewer, **not** credit or fraud decisions — those always rest with the lender.

No open-source licence has been applied yet; all rights are reserved by the authors
pending a decision. Please get in touch before reusing the code.

---

© 2026 FinRiskLens AI · Team ASM FinTech Developer · Google AI Builder Cup 2026
