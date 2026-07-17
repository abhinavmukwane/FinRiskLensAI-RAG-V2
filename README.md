# FinRiskLensAI — AI-Powered MSME Financial Health Score

**FinRiskLensAI** is an AI/ML platform that gives India's **New-to-Credit (NTC)** and
**New-to-Bank (NTB)** MSMEs a credit-worthiness score built from their existing digital
footprint — instead of the bureau history they don't have. It aggregates consent-based
alternate data (Udyam, GST, ITR, Account Aggregator bank statements, MCA), computes a
**0–1000 Financial Health Score across six weighted dimensions**, explains every score,
and surfaces it as a visual **Financial Health Card** for IDBI Bank credit officers.

Built for **IDBI Innovate 2026** (Problem Statement 3 — Financial Health Score) by team
**ASM FinTech Developer**.

## Links

| | |
|---|---|
| 🌐 **Live site** | https://finrisklensai.com/ |
| 🎥 **Demo video** | https://video.finrisklensai.com/ &nbsp;·&nbsp; [Google Drive](https://drive.google.com/file/d/1XT7sGQz7xUhoUDwbNTN6Qij5t8e_v5ea/view?usp=sharing) |
| 📄 **Submission deck (PDF)** | [`Doc/ASM FinTech Developer_Prototype Submission_IDBI Innovate.pdf`](Doc/ASM%20FinTech%20Developer_Prototype%20Submission_IDBI%20Innovate.pdf) |
| 📚 **Full docs** | [`Doc/00_README.md`](Doc/00_README.md) → [`Doc/08_ML_ENGINE.md`](Doc/08_ML_ENGINE.md) |

## The problem

India has ~64 million MSMEs contributing ~30% of GDP, yet 80%+ lack formal credit
access. NTC/NTB enterprises are routinely rejected despite genuine creditworthiness —
simply because they have no bureau file. FinRiskLensAI replaces the *"no document, no
credit"* rejection cycle with an automated, explainable score built from data the
business already generates.

## What it does

- **Udyam-first onboarding** — the MSME enters only its Udyam number; identity, PAN,
  GSTIN, mobile and enterprise details flow from the Udyam response. Email-OTP login.
- **Consent-based data aggregation** — GST returns (GSTR-1/3B/2A/2B), ITR filings,
  Account Aggregator bank & UPI statements (via **Finvu**), Udyam registry, and **MCA**
  company/director/charge data.
- **6-dimension score (0–1000)** — Revenue Vitality (25%), Cash Flow Health (20%),
  Transaction Trustworthiness (15%), Compliance (15%), Business Stability (15%), Debt
  Serviceability (10%); missing sources redistribute weight instead of zeroing a
  dimension, and NTC borrowers get a neutral baseline rather than a penalty.
- **Explainable & audit-ready** — per-dimension reasons via Permutation Feature
  Importance (PFI); a 3-way GST-vs-ITR-vs-bank cross-check flags data-integrity/fraud
  signals separately from creditworthiness.
- **Financial Health Card** — score gauge, dimension radar/bars, strengths & risks,
  bank-lending assessment (indicative WC / term-loan eligibility), GST deep-dive, bank
  statement analysis, MCA charges, and credit-product recommendations (CGTMSE / MUDRA /
  standard working capital).
- **Lender-ready surfaces** — designed for IDBI's LOS and ULI DSP / OCEN 2.0 integration.

## Tech stack

- **Backend:** ASP.NET Core 8 (C#), single MVC web app; Autofac (DI), Serilog (logging).
- **AI / ML:** **ML.NET** in-process — **LightGBM** calibration (final score = 0.6 rules
  engine + 0.4 model), **SSA** (Singular Spectrum Analysis) cashflow trend, **RandomizedPCA**
  anomaly detection, **PFI** explanations. Models train once at startup on synthetic
  profiles.
- **Data & storage:** SQL Server + **EF Core 8** (code-first migrations) for MSME
  profiles, consent/OTP and audit tables; **Azure Blob Storage** for the per-MSME data
  folder (raw source JSON + `result.json`).
- **Frontend:** Razor Views (`.cshtml`) + Bootstrap 5 + Chart.js, minimal JS.
- **Integrations:** Account Aggregator (Finvu / FinFactor), GST, ITR, Udyam, MCA; email
  via MailKit (STARTTLS).

## Architecture (layering)

```
FinRiskLensAI            → Web / MVC (controllers, Razor views, Program.cs, config)
FinRiskLensAI.Services   → application / business logic  (*Service, auto-registered)
FinRiskLensAI.ML         → ML.NET scoring engine + Azure Blob store + feature extractors
FinRiskLensAI.Data       → EF Core, repositories (*Repository, auto-registered)
FinRiskLensAI.Core       → domain models, interfaces, enums (no infrastructure deps)
```

Dependencies point inward toward `Core`. Business flows and the ML engine are documented
in the `Doc/` folder (`00_README.md` through `08_ML_ENGINE.md`).

## Running locally

Requires **.NET 8 SDK**. Configuration (DB connection, Azure Blob, SMTP, Finvu AA) lives
in `FinRiskLensAI/appsettings.json`.

```bash
dotnet restore
dotnet ef database update --project FinRiskLensAI.Data --startup-project FinRiskLensAI
dotnet run --project FinRiskLensAI
```

Then open the printed `https://localhost:…` URL.

## Repository layout

- `FinRiskLensAI/` — web app (controllers, views, wwwroot, `appsettings.json`)
- `FinRiskLensAI.Services/`, `FinRiskLensAI.Data/`, `FinRiskLensAI.Core/`, `FinRiskLensAI.ML/` — layers above
- `Doc/` — product/business documentation, submission deck (PDF + PPTX)

---

©2026 FinRiskLensAI · Team ASM FinTech Developer · IDBI Innovate 2026
