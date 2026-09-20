# FinRiskLensAI — MSME Financial Health Score

**A credit score for the 80% of Indian MSMEs that don't have one.**

FinRiskLens AI scores **New-to-Credit (NTC)** and **New-to-Bank (NTB)** micro, small and
medium enterprises from the digital exhaust they already produce — Udyam registration, GST
returns, income-tax filings, Account Aggregator bank statements and the MCA registry —
instead of the credit-bureau history they have never had. It produces an explainable
**0–1000 Financial Health Score across six weighted dimensions**, an indicative lending
assessment, and a bank-officer workspace that can raise the case straight into a Loan
Origination System, RBI's ULI, or ONDC.

Built for **IDBI Innovate 2026** (Problem Statement 3) by team **ASM FinTech Developer**.
**Shortlisted in the top 5.**

---

## Links

| | |
|---|---|
| 🌐 **Live application** | <https://finrisklensai.com/> |
| 🎥 **Demo video** | <https://video.finrisklensai.com/> · [Google Drive](https://drive.google.com/file/d/1Np-XHCR8K5PcDptUsK_JRyiPNLkuStej/view?usp=sharing) |
| 📊 **Final submission deck** | [PDF](Doc/ASM%20FinTech%20Developer_Final%20Submission_IDBI%20Innovate.pdf) · [PPTX](Doc/Final%20Submission%20Deck%20_%20IDBI%20Innovate.pptx) |
| 📚 **Documentation** | [`Doc/00_README.md`](Doc/00_README.md) — index of all 9 documents |
| 🧠 **How the ML works** | [`Doc/08_ML_ENGINE.md`](Doc/08_ML_ENGINE.md) |

---

## Try it yourself

The live site is seeded with **synthetic demo enterprises** that span the score range. No
mailbox is needed — request an OTP and the app displays it on screen.

1. Go to <https://finrisklensai.com/Auth/CustLogin>
2. Enter one of the emails below → **Generate OTP** → read the OTP off the screen

| Enterprise | Score | Band | Login |
|---|---|---|---|
| Shreeji Precision Components | **813** | Excellent | `shreeji.precision@finrisklens.demo` |
| Sankalp Autotech Pvt Ltd | **806** | Excellent | `sankalp.autotech@finrisklens.demo` |
| Ratnadeep Textile Traders | **627** | Fair | `ratnadeep.textiles@finrisklens.demo` |
| Navdeep Auto Spares | **376** | At Risk | `navdeep.autospares@finrisklens.demo` |

Sankalp is a private limited company, so it is the one that shows the **MCA / director /
charges** screens. Full profiles, dimension breakdowns and the story behind each account
are in [`Doc/DEMO_ACCOUNTS.md`](Doc/DEMO_ACCOUNTS.md).

> The **bank officer portal** (`/Auth/BankLogin`) is a separate, credentialed surface.
> Credentials are shared with the evaluation panel directly rather than published here.

---

## The problem

India has roughly **64 million MSMEs** producing about **30% of GDP**, and more than
**80% of them cannot access formal credit**. The blocker is usually not creditworthiness
— it is evidence. A lender asks for a bureau file; a first-time borrower doesn't have
one; the application dies there.

But that same enterprise has been filing GST returns for years, filing income tax, taking
payments through a bank account, and is registered on Udyam and often on MCA. The evidence
exists. It is just not in the format a credit process expects.

FinRiskLens AI turns that evidence into a score a credit officer can act on — and shows
its working, so the officer can disagree with it.

---

## What it does

**Onboarding — one number, no paperwork**
The MSME enters only its **Udyam registration number**. Legal name, constitution, PAN,
GSTIN, address, mobile and activity codes all come back from the registry. Login is
email-OTP. Every session is screened for VPN / proxy / Tor origin before any data is
fetched.

**Consent-based aggregation — five sources, all opt-in**
GST returns (GSTR-1 / 3B / 2A / 2B), ITR filings, **Account Aggregator** bank and UPI
statements through the RBI framework (via Finvu — we never see a password or a PDF), the
Udyam registry, and **MCA** company, director and charge data. The MCA tab only appears
for constitutions that actually have an MCA record — a partnership firm is shown nothing
rather than something invented.

**Scoring — six dimensions, 0–1000**

| Dimension | Weight | What it reads |
|---|---|---|
| Revenue Vitality | 25% | GST turnover level, growth, seasonality, credit-note reversals |
| Cash Flow Health | 20% | Bank inflow/outflow, surplus, cash cushion, SSA trend |
| Transaction Trustworthiness | 15% | Bounce and return behaviour, counterparty spread, banking penetration |
| Compliance Quotient | 15% | Filing punctuality, GSTR-1 vs 3B consistency, ITC discipline |
| Business Stability | 15% | Enterprise age, product/HSN diversification, B2B trade share |
| Debt Serviceability | 10% | DSCR, FOIR, existing EMI load, MCA charges |

Bands: **≥800 Excellent · ≥650 Good · ≥500 Fair · ≥350 At Risk · below that High Risk.**

Two design choices matter here. When a source is missing its weight is **redistributed
across the sources present**, never zeroed — a missing input reduces confidence, not the
score. And a borrower with **no bureau record gets a neutral baseline, not a penalty**,
which is the entire point for a new-to-credit applicant.

**Explainability — reasons, not a black box**
Every dimension carries plain-language reasons derived from **Permutation Feature
Importance**, and a three-way **GST vs ITR vs bank cross-check** reports data-integrity
and fraud signals *separately* from creditworthiness, so an inconsistency never silently
becomes a low score.

**Financial Health Card**
Score gauge, dimension radar and bars, strengths and risks, a 14-ratio credit appraisal
table, indicative working-capital (Nayak method) and term-loan eligibility, GST deep-dive,
bank-statement analysis, MCA charges, and credit-product recommendations (CGTMSE / MUDRA /
standard working capital). Downloadable as a printable report.

**Score history and trend**
Every analysis is stored **append-only**, so the Health Card and the bank portal show the
*series*, not just today's number. This is the substrate for post-disbursement monitoring
rather than one-time origination scoring — a band that slipped last month is visible.

**Bank officer workspace**
A portfolio dashboard (score distribution, onboarding trend, sector and geography mix,
at-risk and anomaly counts), a searchable customer list, and a **Customer 360** page
carrying every source on its own tab.

**Loan-case hand-off — LOS / ULI / ONDC**
From Customer 360 an officer can raise a scored MSME as a loan case in the bank's own
**LOS**, in RBI's **Unified Lending Interface** (OCEN 4.0 shaped), or on **ONDC** (beckn
protocol, domain `ONDC:FIS12`) — three genuinely different payload shapes behind one
contract. The officer **previews the exact JSON before anything is sent**, and every
attempt is recorded append-only with the score, band and eligibility frozen at push time.

---

## What is real, and what is simulated

Stated plainly, because a public repository invites the question.

| | Status |
|---|---|
| Onboarding, OTP, session security audit | **Working end to end** |
| Scoring engine, six dimensions, ML calibration, explanations | **Working end to end** |
| Financial Health Card, printable report, seven languages | **Working end to end** |
| Bank portal, Customer 360, score history, loan-case push | **Working end to end** |
| Udyam / GST / ITR / MCA data | **Cached and synthetic responses in the documented API shapes** — sandbox access is pending; the Data Field Requirements for all 17 APIs have been submitted |
| Account Aggregator | **Live consent journey** against the Finvu sandbox |
| LOS / ULI / ONDC endpoints | **Not configured** — a push is recorded as `Simulated` carrying the exact payload that would be sent. Setting a URL in config turns the same code path into a real POST. No code change |
| EPFO | Not built — the slot exists in the pipeline |

The application labels simulated pushes as such on screen. Nothing in the UI claims a
live connection that does not exist.

---

## Tech stack

- **Backend** — ASP.NET Core 8 (C#), single MVC application; Autofac for DI with
  convention-based auto-registration; Serilog for structured logging.
- **AI / ML** — **ML.NET**, in-process. **LightGBM** calibration (final score = 0.6 rules
  engine + 0.4 model), **SSA** (Singular Spectrum Analysis) for cash-flow trend,
  **RandomizedPCA** for anomaly detection, **PFI** for explanations. Models pre-train at
  startup off the request path.
- **Data** — SQL Server with **EF Core 8** (code-first migrations) for profiles, consent,
  OTP, score history and audit tables; **Azure Blob Storage** for the per-MSME data folder
  (raw source JSON plus the computed `result.json`).
- **Frontend** — Razor views, Bootstrap 5, Chart.js. Minimal hand-written JS, no SPA
  framework.
- **Integrations** — Account Aggregator (Finvu / FinFactor), GST, ITR, Udyam, MCA; email
  via MailKit over STARTTLS.

## Architecture

```
FinRiskLensAI            →  Web / MVC — controllers, Razor views, Program.cs, config
FinRiskLensAI.Services   →  application and business logic   (*Service,    auto-registered)
FinRiskLensAI.ML         →  ML.NET scoring engine, blob store, feature extractors
FinRiskLensAI.Data       →  EF Core, repositories            (*Repository, auto-registered)
FinRiskLensAI.Core       →  domain models, interfaces, enums (no infrastructure references)
```

Dependencies point inward toward `Core`. `Web` never calls `Data` directly, and `Core`
references no EF Core or infrastructure package. Classes ending in `Service`,
`Repository` or `PayloadBuilder` are registered by convention — adding a loan-case channel
is one new class, no wiring.

---

## Running locally

Requires the **.NET 8 SDK** and a SQL Server instance.

```bash
git clone https://github.com/abhinavmukwane/FinRiskLensAI.git
cd FinRiskLensAI
dotnet restore
```

Configuration lives in `FinRiskLensAI/appsettings.json`, which ships with **placeholder
values only**. Create `FinRiskLensAI/appsettings.Development.json` (git-ignored) with your
own connection string, Azure Blob connection, SMTP and Finvu credentials, then:

```bash
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI
dotnet run --project FinRiskLensAI
```

Open the `https://localhost:…` URL it prints. Every table and the SQL to create it by hand
is logged in [`Doc/DB_SCRIPTS.md`](Doc/DB_SCRIPTS.md).

---

## Documentation

The `Doc/` folder is the real specification, not an afterthought.

| Document | Covers |
|---|---|
| [`00_README.md`](Doc/00_README.md) | Index and implementation ground rules |
| [`01_DOMAIN_MODEL.md`](Doc/01_DOMAIN_MODEL.md) | Entities, relationships, what each layer owns |
| [`02_CUSTOMER_FLOW.md`](Doc/02_CUSTOMER_FLOW.md) | The end-to-end MSME journey, step by step |
| [`03_SCORING_ENGINE.md`](Doc/03_SCORING_ENGINE.md) | How the six-dimension score is computed |
| [`04_API_CONTRACTS.md`](Doc/04_API_CONTRACTS.md) | Internal and external API surface, including the LOS / ULI / ONDC push |
| [`05_INTEGRATIONS.md`](Doc/05_INTEGRATIONS.md) | AA, GST, ITR, EPFO, Udyam — what each is and how we talk to it |
| [`06_BUILD_PLAN.md`](Doc/06_BUILD_PLAN.md) | Build order mapped to the hackathon timeline |
| [`07_SETUP_GAPS.md`](Doc/07_SETUP_GAPS.md) | Known gaps in the scaffold |
| [`08_ML_ENGINE.md`](Doc/08_ML_ENGINE.md) | The ML.NET engine in depth — pipelines, code, extension points |
| [`09_FINALS_ENHANCEMENT_PLAN.md`](Doc/09_FINALS_ENHANCEMENT_PLAN.md) | Verified state, priorities, and what deliberately not to build |
| [`DB_SCRIPTS.md`](Doc/DB_SCRIPTS.md) | Every table created or altered, with the SQL |
| [`DEMO_ACCOUNTS.md`](Doc/DEMO_ACCOUNTS.md) | The seeded demo enterprises and their data |

## Repository layout

```
FinRiskLensAI/            web application — controllers, views, wwwroot, appsettings
FinRiskLensAI.Services/   business logic
FinRiskLensAI.ML/         scoring engine
FinRiskLensAI.Data/       EF Core and repositories
FinRiskLensAI.Core/       domain model
Doc/                      documentation, submission decks, demo data, SQL scripts
```

---

## Team

**ASM FinTech Developer** — a three-member team building for IDBI Innovate 2026,
Problem Statement 3.

## Status and licence

This is a hackathon build under active development for the IDBI Innovate 2026 finals. It
is shared publicly for evaluation. Indicative eligibility figures produced by the platform
are **not** credit decisions — sanction authority always rests with the lender.

No open-source licence has been applied yet; all rights are reserved by the authors
pending a decision. Please get in touch before reusing the code.

---

© 2026 FinRiskLens AI · Team ASM FinTech Developer · IDBI Innovate 2026
