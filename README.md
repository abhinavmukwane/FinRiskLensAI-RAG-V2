# FinRiskLens AI — Evidence-Based Fraud Detection & Intelligent Credit Decisioning

[![Google Cloud](https://img.shields.io/badge/Google%20Cloud-Vertex%20AI-4285F4?logo=google-cloud&logoColor=white)](https://cloud.google.com/vertex-ai)
[![Gemini](https://img.shields.io/badge/Powered%20by-Gemini%203.5%20Flash%20%7C%203.1%20Pro-8E75C2?logo=google&logoColor=white)](https://deepmind.google/technologies/gemini/)
[![Track](https://img.shields.io/badge/Competition-The%20Google%20Cloud%20AI%20Builder%20Cup-EA4335)](https://aibuildercup.com/)
[![Theme](https://img.shields.io/badge/Theme-BFSI%3A%20Intelligent%20Risk%2C%20Fraud%20%26%20Financial%20Experiences-00836C)](#bfsi-theme-alignment)
[![Region](https://img.shields.io/badge/Data%20Residency-Mumbai%20(asia--south1)-005647)](https://cloud.google.com/about/locations/mumbai)

> **Every finding backed by verifiable evidence.**  
> FinRiskLens AI is an end-to-end, multi-source credit risk and fraud intelligence platform for MSME lending. Built for **The Google Cloud AI Builder Cup 2026** under the theme **"BFSI: Intelligent Risk, Fraud & Financial Experiences"**, it unifies document forensics, live registry verification (Account Aggregator, GST, ITR, MCA, Udyam), cross-borrower network fraud detection, and policy-grounded AI credit decisioning into a single audit-ready system.

---

## Quick Navigation

- [Executive Summary](#executive-summary)
- [BFSI Theme Alignment](#bfsi-theme-alignment)
- [Live Demo & Jury Evaluation Guide](#live-demo--jury-evaluation-guide)
- [How It Works: End-to-End Pipeline](#how-it-works-end-to-end-pipeline)
- [Three Pillars of FinRiskLens AI](#three-pillars-of-finrisklens-ai)
  - [1. Multi-Source Risk & Fraud Lens](#1-multi-source-risk--fraud-lens)
  - [2. Autonomous Grounded Investigator Agent](#2-autonomous-grounded-investigator-agent)
  - [3. Personalized Financial Health Experience](#3-personalized-financial-health-experience)
- [Google Cloud Architecture](#google-cloud-architecture)
- [Empirical Benchmarks & Accuracy](#empirical-benchmarks--accuracy)
- [Responsible AI & Regulatory Compliance](#responsible-ai--regulatory-compliance)
- [Tech Stack & Project Layout](#tech-stack--project-layout)
- [Running Locally & Verification](#running-locally--verification)
- [Team & Acknowledgements](#team--acknowledgements)

---

## Executive Summary

MSME lending in India faces a critical paradox: while small enterprises represent the backbone of the economy, lenders face a ₹30+ Trillion credit gap driven by fragmented financial records, escalating digital document tampering (Word, Canva, iLovePDF, spliced balance pages), and syndicate fraud rings that game siloed credit checks.

Traditional fraud prevention tools operate in isolation—analyzing single PDF files with statistical heuristics or black-box machine learning models that generate unexplainable risk scores. When AI is used without strict grounding, it hallucinates figures and false-positive fraud claims that penalize creditworthy businesses and violate banking regulations.

**FinRiskLens AI solves this challenge through an architectural breakthrough:**
1. **Triangulation Across Independent Sources:** Uploaded borrower documents are cross-examined against authoritative government and regulatory APIs—RBI Account Aggregator (AA) raw transaction logs, GST filing returns (GSTR-1, 2B, 3B), Income Tax Returns (ITR), MCA corporate filings, and Udyam registry records.
2. **Deterministic + GenAI Hybrid:** Pure arithmetic checks (running balances, carry-overs, salary slip deductions, loan EMIs) are computed in deterministic, unit-tested code; complex behavioral fraud patterns and cross-document reasoning are evaluated by **Gemini 3.1 Pro** with autonomous tool calling.
3. **Zero-Hallucination Factual Grounding:** Every claim made by the AI must cite specific row/field evidence from that borrower's data and quote verified clauses from the lender's credit policy corpus (retrieved via hybrid `pgvector` HNSW + PostgreSQL full-text search). Unsubstantiated claims are discarded and audited transparently.
4. **Frictionless Open Protocols:** Qualified applications receive an automated, policy-bounded credit appraisal that can be pushed directly to Core Banking / LOS, the **Unified Lending Interface (ULI / OCEN)**, and **ONDC Financial Services**.

---

## BFSI Theme Alignment

FinRiskLens AI directly addresses the four core mandates of the **BFSI: Intelligent Risk, Fraud & Financial Experiences** theme:

| Hackathon Mandate | How FinRiskLens AI Delivers |
|---|---|
| **Detecting & Mitigating Fraud or Risk** | • **10 Core Deterministic Checks (T1–T10)**: Balance breaks, page tampering, declared vs. filed ITR mismatch, round-tripping, window dressing, mule pass-throughs, and undeclared debt obligations.<br>• **Cross-Source Discrepancies**: Compares customer-uploaded PDFs against raw Account Aggregator data and Income Tax records.<br>• **Network Fraud Lens**: Exposes circular trading rings, shared directorships, recycled documents, and multi-borrower application syndicates. |
| **Automating Complex BFSI Processes** | • **Parallel Multimodal Ingestion**: Gemini 3.5 Flash extracts bank statements page-by-page in parallel (10–60s per statement) with 99.8%–100% exact numerical accuracy.<br>• **Hybrid Policy RAG**: PostgreSQL `pgvector` + `tsvector` with Reciprocal Rank Fusion (RRF) and Gemini 3.8 Flash reranking maps violations directly to lender credit policy clauses.<br>• **Automated Underwriting**: Codes compute FOIR debt capacity; Gemini 3.1 Pro drafts justifiable appraisal notes bounded by lender limits. |
| **Improving Customer Interactions** | • **Consent-Driven Self-Service Portal**: Customers connect AA, GST, ITR, and MCA with clear status indicators.<br>• **Data Availability Gating**: The interface intelligently gates advanced risk and score cards until necessary financial data is linked, eliminating onboarding confusion.<br>• **Multi-lingual Accessibility**: Integrated Bhashini / Google Translate interface for inclusive regional language financial access. |
| **Delivering Personalized & Informed Financial Experiences** | • **6-Dimension Financial Health Card (0–1000)**: Evaluates Revenue Vitality, Cash Flow Health, Banking Discipline, Tax Compliance, Business Stability, and Operating Margins.<br>• **Actionable Credit Health Insights**: Provides borrowers transparent visibility into RBI lending ratios (FOIR, DSCR, Current Ratio), indicative working capital limits, and term loan eligibility before loan appraisal. |

---

## Live Demo & Jury Evaluation Guide

| Resource | Link |
|---|---|
| **Live Application** | [https://finrisklensai.cloud.run](https://finrisklensai.cloud.run) |
| **Walkthrough Video Script** | [`Doc/VIDEO_SCRIPT.md`](Doc/VIDEO_SCRIPT.md) |
| **Technical Architecture** | [`Doc/ARCHITECTURE.md`](Doc/ARCHITECTURE.md) |
| **Customer Risk Lens Blueprint** | [`Doc/12_CUSTOMER_RISK_LENS.md`](Doc/12_CUSTOMER_RISK_LENS.md) |
| **Team Tasks & Delivery Log** | [`Doc/13_TEAM_WORK_PLAN.md`](Doc/13_TEAM_WORK_PLAN.md) |

### 5-Minute Guided Evaluation Tour for Judges

1. **Bank Underwriting Portal** (`/Auth/BankLogin`):
   - Navigate to the **Customers** portfolio. Observe the automated categorization: **Clear**, **Review**, **High Risk**, or **Awaiting Data**.
2. **Customer 360 & Risk Lens** (`/BankAdmin/Customer?uan=...`):
   - Select a high-risk borrower (e.g. `UDYAM-MH-99-0000302` or `...0304`).
   - Examine the **Risk Lens**: see the overall verdict followed by source tabs (**Identity**, **GST**, **Banking**, **ITR**, **Network**, **Documents**).
   - Click on any finding to view the **exact evidence**: row number, counterparty, amount mismatch, or PDF metadata trace.
   - Test human-in-the-loop governance: confirm or dismiss a finding with a mandatory credit policy note.
3. **Autonomous Loan Decisioning & Protocol Push**:
   - Open a clean applicant (`UDYAM-MH-99-0000301`).
   - In the **Loan Case** panel, view the code-computed borrowing capacity (FOIR limit).
   - Trigger the **AI Credit Decision** (Gemini 3.1 Pro). Observe how the recommendation stays strictly within capacity and cites policy clauses.
   - Click **Push to LOS / ULI / ONDC** to inspect the structured, machine-readable disbursement payload.
4. **Customer Experience & Financial Health Card** (`/Dashboard/CustDashboard`):
   - Switch to the customer portal. Review the active **Brand Theme** (Classic Maroon & Gold / IDBI Dark Green & Orange).
   - Experience the **Data Availability Gating**: cards appear only when verifiable sources or documents exist.
   - Explore the **Financial Score Card**: interactive score gauge, dimension breakdown, RBI financial ratios, and tailored credit recommendations.

### Synthetic Demo Scenarios (Pre-loaded for Evaluation)

All demonstration records run on realistic synthetic data generated by `tools/FinRiskLensAI.DemoData` with verifiable ground truth:

| Scenario / UAN | Story & Tamper Mechanism | FinRiskLens AI Detection |
|---|---|---|
| **Clear** · `...0000301` | Genuine manufacturer; steady receipts, consistent GST & ITR | **Clear** · Clean Risk Lens → AI Credit Decision Approved → Push to LOS |
| **Edited Statement** · `...0000302` | Tampered PDF: credits inflated; Account Aggregator records real amounts | **High Risk** · Mismatch between uploaded statement and AA bank record |
| **Turnover Inflation** · `...0000303` | Bank credits total 3× the revenue declared in GST & ITR | **High Risk** · Cross-source turnover inconsistency flagged across sources |
| **Filed ITR Mismatch (T8)** · `...0000304` | Uploaded ITR PDF altered; declared income exceeds official filed return | **High Risk** · 5-field comparison detects Gross Income & Tax Paid discrepancy |
| **Mule Account** · `...0000305` | High velocity funds: deposits forwarded in full on the same day | **High Risk** · Pass-through transaction velocity flag triggered |
| **Circular Syndicate** · `...0000306–0308` | Three entities invoicing in loops with a shared common director | **High Risk** · Network Graph Lens flags circular trading & shared director |

---

## How It Works: End-to-End Pipeline

```
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │                     Customer Data Intake & Consent Aggregation                         │
 │   Account Aggregator (AA) · GST Returns · Filed ITR · MCA Registry · Uploaded PDFs     │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ① INGESTION & FORENSICS (Cloud Storage Mumbai · asia-south1)                           │
 │   • Content-addressed SHA-256 storage                                                  │
 │   • PDF metadata sniffer (Producer, ModDate, CreationDate, Font tampering)             │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ② MULTIMODAL EXTRACTION (Vertex AI — Gemini 3.5 Flash · asia-south1)                   │
 │   • Parallel page-by-page extraction (10–60s) with 99.8%–100% exact row accuracy       │
 │   • Preserves verbatim printed numbers (prevents model from "auto-correcting" math)    │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌─────────────────────────────────────────────────────────────────────────────────────────┐
 │ ③ TEN DETERMINISTIC FRAUD CHECKS (C# .NET 8 Engine)                                     │
 │   T1: Balance continuity       T2: AA risk flags             T3: Turnover triangulation │
 │   T4: Identity cross-check     T5: Dismissal governance      T6: Credit capacity limits │
 │   T7: Circular trading links   T8: Uploaded vs Filed ITR     T9: AI Decisioning gate    │
 │   T10: LOS / ULI / ONDC payload integration                                             │
 └──────────────────────────────────────────┬──────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ④ CROSS-BORROWER NETWORK LENS (Entity Resolution)                                      │
 │   • Detects shared directors, phone numbers, emails, registered addresses              │
 │   • Flags byte-identical uploaded documents across distinct applicants                 │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ⑤ INVESTIGATOR AGENT & HYBRID RAG (Vertex AI — Gemini 3.1 Pro & 3.8 Flash)             │
 │   • Scoped function calling: inspects counterparty flows, salary patterns, bounces     │
 │   • Hybrid RAG: PostgreSQL pgvector (HNSW) + tsvector full-text search + Gemini Rerank │
 │   • Grounds every suspicion in specific lender credit policy clauses                   │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ⑥ FACTUAL GROUNDING VERIFIER (Anti-Hallucination Layer)                                │
 │   • Validates every AI claim against raw transaction rows and field data               │
 │   • Discards and logs any hallucinated rupee amounts or uncited policy claims          │
 └──────────────────────────────────────────┬─────────────────────────────────────────────┘
                                            │
                                            ▼
 ┌────────────────────────────────────────────────────────────────────────────────────────┐
 │ ⑦ HUMAN-IN-THE-LOOP RISK LENS & FINANCIAL HEALTH CARD                                  │
 │   • Bank Officer: Risk Lens review, finding confirm/dismiss, LOS/ULI/ONDC dispatch     │
 │   • Customer: 6-Dimension 0–1000 Financial Health Card, RBI borrowing limits           │
 └────────────────────────────────────────────────────────────────────────────────────────┘
```

---

## Three Pillars of FinRiskLens AI

### 1. Multi-Source Risk & Fraud Lens
Rather than relying on isolated document analysis, FinRiskLens AI correlates multiple evidence channels:
- **Registry vs. Upload Cross-Validation:** An altered PDF statement is immediately caught when compared against raw transactions pulled from the Account Aggregator.
- **ITR Discrepancy Engine (T8):** Cross-checks 5 foundational financial metrics across uploaded ITR acknowledgements and official tax filings: Assessment Year, Gross Total Income, Business Turnover, Total Taxable Income, and Tax Paid.
- **Turnover Reconciliation (T3):** Cross-examines GST reported turnover, ITR declared business income, and annualized banking credits to detect artificial turnover inflation or off-the-books diversion.
- **Network Fraud Lens (T7):** Identifies syndicated lending fraud—such as paper companies that share directors, circular invoice loops, or multiple applicants sharing the same bank statement bytes.

### 2. Autonomous Grounded Investigator Agent
- **Reasoning with Scoped Tools:** Powered by **Gemini 3.1 Pro**, the Investigator agent calls sandboxed server-side tools (`get_statement_rows`, `summarize_counterparties`, `get_gst_facts`, `query_credit_policy`).
- **Policy Grounding via Hybrid RAG:** Combines semantic vector similarity (`pgvector` HNSW index on 768-dim embeddings) and exact keyword retrieval (`tsvector` BM25-like search), fused via Reciprocal Rank Fusion (RRF) and reranked by **Gemini 3.8 Flash**.
- **Factual Grounding Verifier:** An automated guardrail inspects all claims output by the model. If a rupee amount or claim does not trace directly to an ingested row or verified policy rule, it is dropped from the report and displayed in the audit trail.

### 3. Personalized Financial Health Experience
- **Customer Financial Health Card:** Translates complex financial data into an intuitive, empowering experience for business owners.
- **6 Key Dimensions (0–1000):**
  1. *Revenue Vitality:* Turnover stability and growth trends.
  2. *Cash Flow Health:* Inflow-outflow buffer and net operating cash flow.
  3. *Banking Discipline:* Inward cheque returns, debit bounces, and balance buffers.
  4. *Tax Compliance:* GST and ITR filing punctuality and consistency.
  5. *Business Stability:* Vintage, corporate entity standing, and operational continuity.
  6. *Operating Margins:* Net profit margins and expense ratios.
- **Lending Assessment & Calculators:** Displays indicative working capital limits, term loan eligibility, and statutory RBI lending ratios (FOIR, DSCR, Current Ratio).

---

## Google Cloud Architecture

FinRiskLens AI is purpose-built to run on enterprise-grade Google Cloud services in the **Mumbai (`asia-south1`)** region to honor strict Indian BFSI data localization and sovereignty standards:

| Google Cloud Component | Purpose & Implementation | Location |
|---|---|---|
| **Vertex AI — Gemini 3.5 Flash** | Multimodal page-by-page document extraction; high-throughput, low-latency OCR and structural parsing. | `asia-south1` (Mumbai) |
| **Vertex AI — Gemini 3.1 Pro** | Complex reasoning agent for fraud investigation and grounded credit decision generation. | Global (Reasoning API) |
| **Vertex AI — Gemini 3.8 Flash** | High-precision cross-encoder reranking of retrieved credit policy and fraud typology passages. | Global |
| **Vertex AI — Embeddings** | 768-dimensional text embeddings (`gemini-embedding-001` / `text-embedding-004`) for hybrid RAG search. | `asia-south1` (Mumbai) |
| **Cloud Run** | Scalable, containerized deployment of the ASP.NET Core 8 enterprise web application. | `asia-south1` (Mumbai) |
| **Cloud SQL for PostgreSQL 16** | Relational persistence for customer cases, findings, and audit trails; native `pgvector` HNSW vector store. | `asia-south1` (Mumbai) |
| **Cloud Storage** | Content-addressed, encrypted blob storage for original PDFs, Account Aggregator logs, and extracted payloads. | `asia-south1` (Mumbai) |
| **Cloud Build & Artifact Registry** | Secure, automated source-to-container build pipeline via Google Cloud buildpacks. | `asia-south1` (Mumbai) |
| **Cloud Logging & Monitoring** | Comprehensive audit logging of credit decisions, tool execution, and grounding verifications. | `asia-south1` (Mumbai) |

---

## Empirical Benchmarks & Accuracy

All performance figures below are derived from automated benchmark runs against ground-truth synthetic evaluation sets (`tools/FinRiskLensAI.DemoData`):

### Extraction Accuracy (17 documents, 1,298 statement rows)
- **Verbatim Row Accuracy (Date, Debit, Credit, Balance):** **99.8% – 100%**
- **Page Balances & Printed Running Totals:** **100% (244/244)**
- **Critical Document Fields (Salary slips, Account numbers):** **100% (38/38)**
- **Extraction Latency:** 10–60 seconds per multi-page statement (parallelized page execution).

### Fraud Detection & Investigator Precision
- **Expected Fraud Scenarios Detected:** **100% (5/5 expected AI findings, 14/14 scenario detections)**
- **False Positive Alarms on Genuine Applicants:** **0 (Zero)**
- **Duplicate Findings:** 0 (Deterministic and AI findings partition cleanly without redundancy).
- **RAG Retrieval Accuracy:** 1.00 MRR on credit policy queries (every test scenario retrieves the exact governing clause).

### Automated Test Coverage
- **106 Automated Tests Passing (`net8.0`):** Covers all 10 deterministic checks, network graph resolution, grounding verification, PDF inspection, and financial ratio calculators.

---

## Responsible AI & Regulatory Compliance

1. **Deterministic Arithmetic:** High-stakes arithmetic (balances, totals, ratios) is never left to generative LLMs; it is strictly computed in verifiable C# code.
2. **Anti-Hallucination Grounding Verifier:** An independent algorithmic verifier checks every claim made by Gemini against raw source facts. Unsubstantiated claims are rejected and logged.
3. **Strict Human-in-the-Loop (HITL):** AI findings do not automatically reject loans. Bank credit officers retain final authority to confirm or dismiss findings with mandatory recorded justifications.
4. **Data Localization & Privacy:** Document processing and database persistence reside in Google Cloud's Mumbai region (`asia-south1`), aligning with the **Digital Personal Data Protection (DPDP) Act 2023** and RBI guidelines.
5. **Open Financial Protocols:** Ready for direct ecosystem integration with **India Stack / ULI (Unified Lending Interface)**, **OCEN**, and **ONDC Financial Services**.

---

## Tech Stack & Project Layout

### Technology Stack
- **Backend Framework:** .NET 8 / ASP.NET Core 8 MVC (C#)
- **Dependency Injection:** Autofac
- **Data & ORM:** EF Core 8, Cloud SQL for PostgreSQL 16 with `pgvector`
- **GenAI & ML:** Google Cloud Vertex AI SDK (Gemini 3.5 Flash, Gemini 3.1 Pro, Gemini 3.8 Flash, Text Embeddings)
- **PDF & Document Processing:** PdfPig, QuestPDF
- **Frontend & UI:** Razor Pages, Vanilla CSS Design System (`theme.css`), Bootstrap 5, Chart.js, Leaflet.js
- **Testing:** xUnit, FluentAssertions, Moq

### Solution Layout

```
FinRiskLensAI-RAG-V2/
├── FinRiskLensAI-RAG-V2/     # Web application (Controllers, Views, Configuration)
├── FinRiskLensAI.Fraud/      # Vertex AI client, Gemini Agent, Checks, Grounding, Hybrid RAG
├── FinRiskLensAI.ML/         # Verification source parsers (AA, GST, ITR, MCA, Udyam)
├── FinRiskLensAI.Services/   # Domain services and business logic orchestration
├── FinRiskLensAI.Data/       # EF Core data context, pgvector repositories, migrations
├── FinRiskLensAI.Core/       # Domain entities, models, and interface contracts
├── tools/
│   ├── FinRiskLensAI.DemoData/ # Synthetic document generation & live benchmark harness
│   └── FinRiskLensAI.RagCli/  # Knowledge corpus ingestion and RAG test CLI
├── tests/
│   └── FinRiskLensAI.Tests/   # 106 automated xUnit tests
└── Doc/                       # Architecture diagrams, build plans, and knowledge base
```

---

## Running Locally & Verification

### Prerequisites
- **.NET 8.0 SDK**
- **PostgreSQL 15+** with the `vector` extension enabled
- **Google Cloud CLI (`gcloud`)** authenticated to a project with Vertex AI and Cloud Storage enabled

### Step-by-Step Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/abhinavmukwane/FinRiskLensAI-RAG-V2.git
   cd FinRiskLensAI-RAG-V2
   ```

2. **Authenticate Google Cloud Application Default Credentials:**
   ```bash
   gcloud auth application-default login
   ```

3. **Configure Database & Secrets:**
   Configure connection string in `FinRiskLensAI-RAG-V2/appsettings.Development.json`:
   ```json
   {
     "Database": {
       "ConnectionStrings": {
         "PostgreSQL": "Host=localhost;Port=5432;Database=finrisklens;Username=postgres;Password=YOUR_PASSWORD"
       }
     },
     "Gcp": {
       "ProjectId": "YOUR_GCP_PROJECT_ID"
     }
   }
   ```

4. **Apply Database Migrations:**
   ```bash
   dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI-RAG-V2
   ```

5. **Seed Knowledge Base & Demo Scenarios:**
   ```bash
   dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Typology Doc/knowledge/typology.md --title "Fraud Typology Catalogue" --version v1
   dotnet run --project tools/FinRiskLensAI.RagCli -- ingest Policy Doc/knowledge/credit-policy.md --title "Sample Credit Policy" --version v3
   dotnet run --project tools/FinRiskLensAI.DemoData -- seed
   dotnet run --project tools/FinRiskLensAI.DemoData -- customers
   ```

6. **Run the Application:**
   ```bash
   dotnet run --project FinRiskLensAI-RAG-V2
   ```
   Navigate to `https://localhost:7052` (or the configured port).

7. **Execute Test Suite (106 tests):**
   ```bash
   dotnet test tests/FinRiskLensAI.Tests
   ```

---

## Team & Acknowledgements

Developed with passion for **The Google Cloud AI Builder Cup 2026** (BFSI Track) by **Team AS FinTech Developer**:

- **Abhinav Mukwane** — Architect, Backend & Vertex AI Integration, Core Engine, Bank Admin
- **Sourabh Thakre** — Credit Risk Checks, Financial Health Engine, Demo Datasets & UX, ChatBot

*Special thanks to Google Cloud and the Vertex AI team for providing the next-generation foundation models that made zero-hallucination evidence-grounded lending intelligence possible.*

---

© 2026 FinRiskLens AI · Team AS FinTech Developer · Google Cloud AI Builder Cup 2026
