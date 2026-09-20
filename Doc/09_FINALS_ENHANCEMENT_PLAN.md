# Finals Enhancement Plan — IDBI Innovate 2026 (Top 5)

**Status:** shortlisted in the top 5. This document is the build plan for the final round.
**Team:** ASM FinTech Developer (3 members).
**Scope:** what to add, why it wins, what it costs, and — equally important — what NOT to build.

Companion docs: [`00_README.md`](00_README.md) (product), [`08_ML_ENGINE.md`](08_ML_ENGINE.md)
(scoring internals), [`DB_SCRIPTS.md`](DB_SCRIPTS.md) (schema log), and the IDBI
data-field submission (`IDBI Innovate 2026 - Data Field Requirements - FinRiskLens AI.docx`).

---

## 0. Where the prototype actually stands

Verified against the codebase, not the deck:

| Area | State |
|---|---|
| Onboarding | Udyam-first, email OTP, SQL-backed session, duplicate-registration guard |
| Data sources | Udyam, GST (7 return types incl. GSTR-2B), ITR (ITR-1/ITR-3, 3 years), AA (Finvu), MCA + DIN |
| Scoring | 6 dimensions / 0–1000, weight redistribution, NTC neutral baseline |
| ML | LightGBM calibration (0.6 rules + 0.4 model), SSA cashflow trend, RandomizedPCA anomaly, PFI explanations, startup warm-up |
| Underwriting | Lending calculator (Nayak WC + term-loan annuity), 14-ratio appraisal table, 4 deep-dive sections |
| Surfaces | Customer dashboard, Financial Health Card, printable Financial Report, Bank portal (Customer 360 + list + dashboard), Groq chatbot, IP-risk audit |
| Persistence | SQL Server + EF Core 8, Azure Blob per-MSME folder, `MsmeScoreSummary` SQL projection |
| Ops | DataProtection keys persisted, SQL session cache, parallel blob download, Serilog |
| Monitoring | **Added 2026-09-08** — `t_MsmeScoreHistory` (append-only) + Score History trend chart on the Health Card and Customer 360 |
| Hand-off | **Added 2026-09-15** — loan-case push to LOS / ULI / ONDC from Customer 360 (preview → confirm → receipt, `t_LoanCasePush`) |

That is a genuinely strong prototype. The gaps below are not "missing features" —
they are the specific things a **banking jury** will probe.

---

## 1. Critical — the gap that can sink the demo

### 1.1 AA data does not reach the score

**Evidence:** `FinancialInfoFetch` persists
Finvu's **encrypted** FI response to `aa.json`; `AaFeatureExtractor` expects decrypted
`body[*].fiObjects[*]`. `BlobAnalysisService.AnalyzeAsync` logs a "no fiObjects"
warning and scores without bank data.

**Why it matters:** Cash Flow Health (20%) and Debt Serviceability (10%) are the two
dimensions a banker cares about most. Today they run on redistributed weight, i.e.
**30% of the score is derived from a source we demo but do not consume.** The first
question from the jury will be *"so the bank statement drives the cash-flow dimension?"*

**Two paths:**

| Option | What | Effort |
|---|---|---|
| A — demo-safe | Seed a plaintext AA fileset the way `DUMMY-DATA` already seeds GST; label it clearly as sandbox data | ~4 hours |
| B — proper | ECDH decryption using the `KeyMaterial` returned in the FI response; write decrypted `body[*].fiObjects[*]` to `aa.json` | 2–3 days |

**Decision: do A immediately** so the live demo is coherent, then B if runway allows.
Also add FIStatus polling — the first FI fetch is usually `PENDING` and returns empty.

> Nothing else in this document matters as much as closing this.

---

## 2. Tier 1 — build before the finals

### 2.1 Score history + Early Warning System ⭐ highest ROI

> **Status 2026-09-08 — half shipped.** The table and the trend chart are built;
> the EWS rules on top of them are not. See "Build" below for which bullet is
> which.

**Problem:** `MsmeScoreSummary` is one row per UAN, **upserted** — every re-analyze
destroys the previous value. There is no history, therefore no trend, therefore no
monitoring.

**Build:**
- ~~`MsmeScoreHistory` — append-only table, one row per computed score (UAN, all six
  dimension scores, overall, band, `ComputedAt`, model version).~~ **Done** —
  `t_MsmeScoreHistory`, appended by `AnalyzeAsync` on every run; the six dimensions
  are columns, not JSON, precisely so an EWS rule can be a `WHERE` clause. Rows carry
  `Source` (`analysis` / `seed`). See `DB_SCRIPTS.md`.
- ~~Score trend line on the Financial Health Card.~~ **Done** — a **Score History**
  button on the Health Card and on bank Customer 360 opens a modal with a Chart.js
  line (overall + cash-flow health + compliance, points coloured by band). Served by
  `/score-history`, which reads the bank session when an operator supplies a UAN and
  the customer session otherwise.
- **EWS panel on the bank portal:** *"14 accounts dropped a band this month."* —
  **still to build**, and now the cheap half: the series it compares against exists.
  Triggers: GST filing lapse, turnover fall >25% QoQ, new MCA charge registered,
  cheque-return spike, AA balance trend inversion, band downgrade.

**Why it wins:** scoring at origination is a nice-to-have; **continuous monitoring
post-disbursement is a P&L line item.** It reframes the product from "we help you
underwrite" to "we help you not lose money." Every one of those signals is already
computed by the existing extractors — the only thing missing is storing yesterday's
value to compare against.

**Effort: 2–3 days.**

### 2.2 Integration API — Swagger + JWT

**Problem:** `Microsoft.AspNetCore.Authentication.JwtBearer` is referenced in the
csproj and `Program.cs` calls `app.UseAuthentication()` — but there is **no
`AddAuthentication` / `AddJwtBearer` registration anywhere.** That middleware line is
a no-op. `POST /api/scoring/analyze` and `/api/msme-data/{uan}` are unauthenticated
and undocumented.

**Build:**
- Wire the JWT bearer scheme that is already referenced; issue per-consumer API keys.
- Add Swashbuckle; publish OpenAPI at `/swagger`.
- Version the routes (`/api/v1/...`) and document request/response contracts.
- Map the endpoints explicitly to the four integration questions the mentors asked:
  **LOS** (score pull at application), **LMS** (monitoring webhook),
  **OCEN/ULI** (DSP-shaped score API), **ONDC** (transaction-data ingest).

> **Partly answered 2026-09-15.** The *outbound* half of the LOS / ULI / ONDC
> question is built — the bank portal raises a loan case in any of the three, each
> with a standard-shaped payload, endpoint read from config
> (`04_API_CONTRACTS.md` §4a). What remains here is the *inbound* half: a secured,
> versioned, Swagger-documented endpoint a lender calls to pull a score.

**Why it wins:** the answer to all four mentor questions is a single artefact.
*"Here is the OpenAPI spec your LOS team codes against, live, right now"* beats three
architecture slides.

**Effort: 1 day.**

### 2.3 Credit policy / decision layer

**Problem:** we output a score. A bank needs a **decision**.

**Build:** a policy console in the bank portal —
- Product-wise cut-offs (CGTMSE / MUDRA / working capital): Approve ≥720,
  Refer 620–719, Decline <620 — all editable, not hard-coded.
- Hard knock-outs: GSTIN cancelled, director disqualified, MCA charge above threshold.
- Deviation capture: officer override with mandatory reason, logged and reportable.
- Output: **Approve / Refer / Decline + indicative limit + sanction conditions.**

**Why it wins:** converts an analytics tool into a lending system. Sits entirely on
outputs already computed — it is a rules table plus a screen.

**Effort: 2 days.**

### 2.4 PDF Credit Appraisal Memorandum

**Problem:** `ReportController.FinancialReport` renders HTML for browser print. A
credit file needs an archivable, shareable PDF.

**Build:** QuestPDF (free under its community licence at our revenue) generating a
proper CAM — score + six dimensions, 14-ratio appraisal table, lending eligibility,
bank-statement and GST deep-dives, **data-provenance appendix** (which source, pulled
when, consent artefact ID), model version, timestamp.

**Why it wins:** hand the jury a printed copy. Physical artefacts land
disproportionately well, and the provenance appendix pre-answers the audit question.

**Effort: 1 day.**

### 2.5 DPDP-compliant consent ledger

**Problem:** `t_AAConsentRequest` records AA consent only. There is no unified consent
artefact across sources, no revocation path, no retention/purge job.

**Build:**
- One consent ledger spanning GST / ITR / AA / Udyam — purpose, scope, expiry,
  artefact ID, timestamp.
- Customer-facing **"My Consents"** screen with a working **Revoke**.
- Auto-purge on expiry or revocation, leaving an audit stub that proves deletion.

**Why it wins:** the DPDP Act 2023 is in force. Every bank legal team asks. Unticked,
this becomes a jury objection rather than a differentiator.

**Effort: 1.5 days.**

---

## 3. Tier 2 — differentiators if time allows

| # | Item | Why it wins | Effort |
|---|---|---|---|
| 3.1 | **Model card + governance page** | RBI expects model risk management. One page: version, training data, feature list, PFI importances, drift metrics, override rate. Says "auditable" | 1 day |
| 3.2 | **Sector percentile benchmarking** | "719 — 78th percentile among NIC 61xx enterprises in Maharashtra." Context beats a bare number for a credit officer | 1 day |
| 3.3 | **Hindi + one regional language** | We pitch all-India branch rollout; a language toggle proves we designed for a Nanded branch, not only Mumbai | 1 day |
| 3.4 | **Rule-based AML / fraud screen** | Circular trading (same GSTIN as both customer and vendor), round-tripping, cash-intensity outliers, shell-company signals from MCA — all computable from data already held | 1.5 days |
| 3.5 | **Bureau hybrid for NTB** | Our pitch is NTC, but IDBI's real book is NTB with thin bureau files. Show the score blending a bureau pull when one exists; a mocked CIBIL adapter proves the design | 1 day |
| 3.6 | **Portfolio analytics for the bank** | Score distribution, sector concentration, geography heatmap, approval funnel — makes the bank portal a product, not a demo | 1 day |

---

## 4. Tier 3 — roadmap slide only, do not build

Credibility without burning finals time:

- **EPFO** — employee-count and PF-remittance regularity (the `epfo.json` /
  `HasEpfo` slot is already stubbed).
- **ULI / OCEN 2.0** DSP *certification* path (the outbound case hand-off is
  built — certification, mTLS and registry onboarding are the roadmap part).
- **ONDC / GeM / e-NAM** transaction-history pulls (again: we can *place* a beckn
  order; ingesting their transaction history is the roadmap part).
- **UPI merchant settlement** data for micro-enterprises.
- **Geospatial / satellite** signals for agri-MSME.
- **Fleet telematics** for logistics MSMEs.
- **Co-lending orchestration** with NBFC partners.

---

## 5. What NOT to build

Each of these costs days and wins nothing:

- **More dashboards.** We have enough; the jury saw them in round one.
- **A mobile app.** Responsive web is sufficient, and an app invites
  *"where's the Play Store listing?"*
- **Deep learning / LLM-based scoring.** The LightGBM + rules blend is *more*
  defensible to a regulator than a neural net. Explainability is a feature here —
  do not trade it for buzzwords.
- **More data sources.** We have six, and one of them (AA) is not yet wired into the
  score. Depth beats breadth.

---

## 6. Recommended sprint — if we only do four things

| Order | Item | Section | Effort |
|---|---|---|---|
| 1 | Wire AA into the score | 1.1 | 0.5–3 d |
| 2 | ~~Score history~~ + EWS | 2.1 | history **done**; EWS 1 d |
| 3 | Swagger + JWT on the API | 2.2 | 1 d (outbound push **done**) |
| 4 | PDF appraisal memo | 2.4 | 1 d |

**~6 working days across 3 members — one comfortable sprint.** Everything else is upside.

---

## 7. The strategic shift

We currently pitch: *"we score NTC MSMEs."*
**Every finalist will pitch a score.**

Pitch instead:

> **"We score them, we keep watching them, and we plug into your LOS on day one."**

As of 2026-09-15 all three clauses are demonstrable on screen, not just on a slide:
the score, the Score History chart, and a loan case raised into LOS / ULI / ONDC
with the exact payload shown before it is sent.

Origination + monitoring + integration is a **system a bank can buy**.
A score is a feature.

---

*Doc/09 · ASM FinTech Developer · IDBI Innovate 2026*
