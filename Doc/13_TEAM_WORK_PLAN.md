# 13 — Team work plan: Oct 16 – Oct 18

The one page both of us work from until submission. **What** we are building is in
[`12_CUSTOMER_RISK_LENS.md`](12_CUSTOMER_RISK_LENS.md); **how it fits together** is in
[`ARCHITECTURE.md`](ARCHITECTURE.md). This document says **who does what, where in the code,
and how we know it is done**.

**How to use it:** take the next task in your column on the board, set its status, work on a
branch, open a pull request when its *Done when* list is all true, and tick it here in the same PR.

---

## 1. Board

**A** = Abhinav (lead: production, deploys, secrets, core engine) · **B** = Sourabh (checks,
calculator, demo data, docs, user-facing content and UI copy — §8). Status: ⏸ to do · 🔨 in
progress · 👀 in review · ✅ done.

| ID | Task | Owner | Day | Needs | Status |
|---|---|---|---|---|---|
| M1 | Customer uploads, risk per customer, Risk Lens tab, Cases menu out | A | Oct 15 | — | ✅ |
| S0 | Set up a working copy (§3) and click through the Risk Lens locally | B | Oct 16 | — | ⏸ |
| T1 | Every registered customer has an assessment; re-assess when a source arrives | A | Oct 16 | — | ✅ |
| T2 | Account Aggregator risk flags become findings | A | Oct 16 | T1 | ✅ |
| T3 | Turnover check: GST vs ITR vs bank credits | B | Oct 16 | T1 (merge) | ✅ done |
| T4 | Identity check: Udyam, GST, ITR and MCA agree; company active | B | Oct 16 | T1 (merge) | ✅ done |
| T5 | Dismissing a finding needs a written reason (policy §6.3) | B | Oct 16 | — | ✅ done |
| T6 | Credit capacity calculator: FOIR and eligible limit (policy §3) | B | Oct 16–17 | — | ✅ |
| T7 | Network links for businesses: shared director, contact, address; circular trading | A | Oct 17 | T1 | ✅ |
| T8 | Uploaded ITR vs the filed return | B | Oct 17 | T1 | ✅ done |
| T9 | AI credit decision: service, `credit_decisions` table, Risk Lens loan-case panel | A | Oct 17 | T6 | ✅ unblocked & live ready |
| T10 | Gate the loan-case push; risk and decision blocks in the LOS, ULI and ONDC payloads | A gate · B payloads | Oct 17 | T9 | ✅ done (gate & payloads) |
| T11 | Demo customers: generator, registrations, sources, uploads | B builds · A seeds prod | Oct 17–18 | T2–T8 | ✅ done |
| T12 | Live eval and investigation re-run; numbers into README | A | Oct 18 | T11 | ⏸ |
| T13 | README and ARCHITECTURE to the customer flow | B | Oct 18 | T11 | ✅ done |
| T14 | Deck and video script to the customer flow; record the video | A deck · B records | Oct 18 | T11 | 🔨 deck & script updated · record ⏸ |
| T15 | Deploy, smoke test, **submit** | A | Oct 18 | all | ⏸ |
| C1 | Content guidelines (`CONTENT_GUIDELINES.md`); onboarding, Sign In and dashboard copy | B | Oct 15–16 | — | ✅ |
| C2 | Home page: headline, hero, feature cards | B | Oct 16 | C1 | ✅ |
| C3 | International terminology (approved Oct 17): customer dashboard, sidebar, Udyam page | B | Oct 17 | C1 | ✅ |
| C4 | GST page: one fixed sample dataset, unsupported claims removed; ITR page wording | B | Oct 17 | C1 | ✅ |
| C5 | MCA director page; OFAC and AML board screening in demo mode | B | Oct 17 | C1 | ✅ |
| C6 | Account Aggregator page; Risk Lens and bank pages (with A) | B | Oct 18 | C1 | ⏸ |
| C7 | Home page Product Guide: public assistant on Gemini Flash that explains the product, no customer data | B | Oct 18 | C2 | 👀 |

**Order of play.** Oct 16: T1–T6. Oct 17: T7–T10, start T11. Oct 18: finish T11, then
T12–T15. Submit **by the evening of Oct 18**, with the deploy done by midday so there is time to
fix anything the smoke test finds. The cut list is in §6.

**Status, evening of Oct 17.** B's Oct 17 went to content (C3–C5, §8), pushed to `main` directly.
T3, T4, T5, T6, T8, the T10 payloads, T11 and T13 have not started. T6 holds up T9's live run; T11
holds up T12, the deck's last screenshots (T14) and the video. A demo customer whose story needs a
check that is not built (T3 turnover inflation, T8 edited ITR) cannot show *High risk*: if that
check is cut, drop the customer from T11, from the deck's demo slide and from the README.

---

## 2. Roles and access

| | A — Abhinav | B — teammate |
|---|---|---|
| Owns | Production database, Cloud Run deploys, `appsettings.Production.json`, merges to `main` | Checks, calculator, demo generator, docs |
| Google Cloud | Owner | Vertex AI (Gemini), read/write on the `cases` and `sources` buckets, logs, Cloud Run and Cloud SQL viewing |
| Database | Development (Neon) and production (Cloud SQL, through the proxy) | Development (Neon) only |
| Cannot | — | Deploy, or reach production data. **Never needs to.** |

---

## 3. Getting set up (B)

1. Clone `abhinavmukwane/FinRiskLensAI-RAG-V2`. Install the **.NET 8 SDK** and the **gcloud CLI**.
2. Put the `appsettings.Development.json` that A sends privately into `FinRiskLensAI-RAG-V2/`
   (next to `appsettings.json`). It is git-ignored — **never commit it**.
3. Sign in for Gemini and Cloud Storage:
   ```bash
   gcloud auth application-default login
   ```
   ```bash
   gcloud auth application-default set-quota-project finrisklensai
   ```
4. Run the tests — all must pass before and after every change:
   ```bash
   dotnet test tests/FinRiskLensAI.Tests
   ```
5. Run the app and open <http://localhost:5086>:
   ```bash
   dotnet run --project FinRiskLensAI-RAG-V2 --launch-profile http
   ```
   - **Bank portal:** `/Auth/BankLogin`, user `bankadmin` — A shares the password privately.
   - **Customer side:** `/Auth/CustLogin` with a registered customer's email. In development the
     one-time code is shown on screen (the app also emails it).
6. The demo tool, from the repository root:
   ```bash
   dotnet run --project tools/FinRiskLensAI.DemoData -- generate <dir>
   ```
   `generate` writes the demo PDFs and checks them in memory (free). `seed` writes them to the
   **development** database. `eval` and `investigate` call Gemini — they cost money, so run them
   for the cases you changed, not all of them.

---

## 4. Working rules

- **Branch per task:** `task/T3-turnover`, cut from the latest `main`. `git pull --rebase` before
  pushing. Small pull requests; the other person reviews within a couple of hours; A merges.
- **Commit messages** start with `#` and say what changed: `#Turnover check: GST vs ITR vs bank`.
- **Secrets:** both settings files are git-ignored; nothing secret goes in code, docs or commits.
  The repository is public.
- **Database changes:** write the entry in `Doc/DB_SCRIPTS.md` **first**, then the EF migration.
  A applies it to production.
- **Demo data is synthetic only.** Source data for demo customers goes only into
  `UDYAM-MH-99-*` folders — the sources bucket also holds real businesses' data.
- **Wording:** product text is positive — "fraud detection", "risk analysis", "every finding
  backed by evidence". No "not a score"-style contrasts.
- **A new finding type needs:** a type name in capitals (`TURNOVER_MISMATCH`), a line in
  `RiskSections.Of` (`FinRiskLensAI.Core/Models/Cases/CaseViewModels.cs`) so it lands in the right
  Risk Lens section, an explanation in rupees, at least one piece of evidence, and a unit test.
- **Thresholds are settings**, in `FraudOptions` (`FinRiskLensAI.Fraud/Checks/DeterministicChecks.cs`),
  never constants buried in a check.

### Where things live

| What | File |
|---|---|
| Per-document checks (balances, totals, slips, metadata, bank-record comparison) | `FinRiskLensAI.Fraud/Checks/DeterministicChecks.cs` |
| Links between customers (network) | `FinRiskLensAI.Fraud/Checks/NetworkLens.cs` |
| Where the checks run for an assessment | `CaseService.RunChecksOnceAsync` in `FinRiskLensAI.Fraud/Cases/CaseService.cs` |
| Background pipeline: read → check → investigate | `FinRiskLensAI.Fraud/Pipeline/CasePipeline.cs` |
| The customer's facts from GST, ITR, AA, Udyam | `IBlobAnalysisService.GetFactsAsync(uan)` → `SourceFacts` / `MsmeFeatureSet` |
| Account Aggregator analysis (with `RiskFlags`) | `FinRiskLensAI.Services/Implementation/AccountAggregator/AaStatementAnalysisService.cs` |
| Verdict rule and Risk Lens sections | `RiskSummary`, `RiskSections` in `FinRiskLensAI.Core/Models/Cases/CaseViewModels.cs` |
| Risk Lens page | `FinRiskLensAI-RAG-V2/Views/Shared/_RiskLensBody.cshtml` (+ `_FindingCard.cshtml`) |
| Loan-case push and payloads | `LoanCaseController`, `FinRiskLensAI.Services/Implementation/LoanCase/` |
| AI investigator and grounding | `FinRiskLensAI.Fraud/Ai/Investigator.cs`, `GroundingVerifier.cs` |
| Demo data | `tools/FinRiskLensAI.DemoData/Scenarios.cs`, `Render.cs`, `Program.cs` |

---

## 5. Tasks

### T1 — Every registered customer has an assessment; re-assess when a source arrives (A)

**Why.** Today an assessment is created by the first upload, so a customer who only fetched
bank data shows *Awaiting data*, and the source checks (T2–T4) would have nowhere to run.

**Steps**
1. `ICaseService.EnsureAssessmentAsync(uan, enterpriseName, actor)` → the customer's assessment
   id, creating an empty one if none exists (reuse the creation code in
   `AddCustomerDocumentsAsync`).
2. Call it and queue the pipeline (`CaseQueue.Enqueue`) when a source arrives:
   - **Account Aggregator** — where `HomeController.AAConsentCallback` gets `dataStored == true`
     from `FetchAndStoreFinancialData` (around line 67);
   - **GST, ITR, MCA** — at the end of `SeedFinancialData`, `SeedItrData` and `SeedMcaData`
     in `DashboardController`.
3. The pipeline already handles an assessment with no documents: it runs the checks and the
   investigator. Confirm it does not fail on zero documents.

**Done when**
- A registered customer with Account Aggregator data and no uploads shows a verdict other than
  *Awaiting data* on the Customers list and the Risk Lens.
- Fetching a source re-runs the assessment.

**✅ Done 2026-10-16.** `EnsureAssessmentAsync`; re-assessment after AA consent stores the bank data
(`HomeController`), and after GST, ITR or MCA data actually changes (`DashboardController`; a failure
is logged and never fails the fetch); an **Assess shared data** button on the Risk Lens for customers
whose data arrived earlier. Found on the way: an assessment with nothing about the bank would have read
*Clear*. The verdict now needs **bank evidence** — the AA record read, or a statement read from an upload
(policy §5.1) — else *Awaiting data*. New column `applications.bank_data_at` (`DB_SCRIPTS.md`,
migration `BankDataAt`, applied to development; production at T15).

### T2 — Account Aggregator risk flags become findings (A)

**Why.** The AA analysis already computes `RiskFlags` (pass-through, heavy cash withdrawal,
negative balance, banking discipline events — each with `Severity`, `Title` and `Evidence`), but
they only show on the AA tab, so they never reach the verdict.

**Steps**
1. In `CaseService.RunChecksOnceAsync`, keep the whole `AaAnalysisResult` (today `BankRecordAsync`
   keeps only `.Transactions`).
2. New pure static class `FinRiskLensAI.Fraud/Checks/SourceChecks.cs` with
   `BankFlags(AaAnalysisResult)`, returning a finding for every `High` or `Medium` flag (skip
   `Positive`). Types: `AA_PASS_THROUGH`, `AA_CASH_DEPENDENCY`, `AA_NEGATIVE_BALANCE`,
   `AA_DISCIPLINE_EVENTS` — map from the flag title. Category `Behavioural`; evidence
   `EvidenceKind.App` with the flag's evidence text; `DedupKey` = the type.
3. Map the four types to `RiskSections.Bank`.

**Done when** the mule-like demo customer (T11) shows `AA_PASS_THROUGH` under **Bank conduct**,
with a unit test for the title-to-type mapping.

**✅ Done 2026-10-16** in `SourceChecks.BankFlags`, run on every assessment with AA data. Nine titles
mapped (a flag the table does not know is still typed from its title, never dropped); one finding per
pattern at its most severe; all `AA_*` types go to **Bank conduct**. The three development customers
with bank data raise only positive indicators, so nothing changed for them; the end-to-end check is the
mule-like demo customer in T11.

### T3 — Turnover check: GST vs ITR vs bank credits (B)

**Why.** Turnover inflated for the application shows up as bank credits far above what GST
returns and the filed ITR report (typology T-07). Compare like with like: GST turnover against
ITR **business turnover**, never against gross total income.

**Inputs** (`MsmeFeatureSet`, from `GetFactsAsync`): `GstMonthlyAvgTurnover`,
`ItrBusinessTurnover` (yearly — divide by 12), `AaMonthlyAvgBankCredit`. The LOS payload
already shows these three side by side (`crossSourceTurnover` in `LosPayloadBuilder.cs`).

**Steps**
1. `SourceChecks.Turnover(MsmeFeatureSet f, FraudOptions o)` → a finding or null.
   - Bank credits above `o.BankToTurnoverMax × max(GST, ITR)` → `TURNOVER_MISMATCH`, High
     (start with `BankToTurnoverMax = 1.5`).
   - GST and ITR turnover more than `o.GstItrVariancePct` apart (start at 25%) → `TURNOVER_MISMATCH`,
     Medium.
   - Skip a leg that is missing; never flag on one source alone.
2. Explanation in rupees per month, for example: *"Bank credits average ₹18,40,000 a month
   against GST turnover of ₹6,10,000 and ITR turnover of ₹5,90,000 — three times what the tax
   filings report."* Evidence `EvidenceKind.App`, one note per figure.
3. Map `TURNOVER_MISMATCH` to `RiskSections.Gst`. Call it from `RunChecksOnceAsync` with the
   customer's facts (inject `IBlobAnalysisService` into `CaseService`, optional like the AA service).

**Done when** unit tests cover: consistent figures → no finding; bank three times GST → High;
GST vs ITR 40% apart → Medium; a missing source → no finding.

**✅ Done 2026-10-18** in `SourceChecks.Turnover`, wired into `CaseService.RunChecksOnceAsync`. `TURNOVER_MISMATCH` mapped to `RiskSections.Gst`. Covered by unit tests in `SourceChecksTests.cs`.

### T4 — Identity check: the sources agree; the company is active (B)

**Inputs:** `udyam.json`, `gst_taxpayer.json`, `itr.json`, `mca.json` in the customer's sources
folder (the profile builder and `SourceFacts` already parse them — reuse, do not re-parse).

**Rules**
| Rule | Finding | Severity |
|---|---|---|
| PAN differs between Udyam and ITR, or between Udyam and GST | `IDENTITY_MISMATCH` | High |
| GSTIN on Udyam differs from the GST record | `IDENTITY_MISMATCH` | High |
| Business name differs after normalising (case, punctuation, PVT LTD / PRIVATE LIMITED, LLP) | `IDENTITY_MISMATCH` | Medium |
| MCA company status is not Active (struck off, dormant, under liquidation) | `COMPANY_INACTIVE` | High |

Map both types to `RiskSections.Identity`. Unit-test the name normaliser on at least five pairs.

**Done when** each rule has a test and the identity demo story (T11) raises the finding.

**✅ Done 2026-10-18** in `SourceChecks.Identity` and `SourceChecks.NormalizeBusinessName`, wired into `CaseService.RunChecksOnceAsync`. Mapped to `RiskSections.Identity`. Covered by 6 business-name normaliser test pairs and full rule tests in `SourceChecksTests.cs`.

### T5 — Dismissing a finding needs a written reason (B)

Credit policy §6.3. In `RiskLensController.Review`, refuse `Dismissed` without a note (return to
the page with a message). In `_FindingCard.cshtml` make the note box `required` for Dismiss only
(the Confirm button keeps it optional).

**Done when** dismissing without a note is refused, and with a note it works and returns to the
Risk Lens.

**✅ Done 2026-10-18** in `RiskLensController.Review`, `_FindingCard.cshtml` with modal validation, and `TempData["Error"]` banner in `_RiskLensBody.cshtml`.

### T6 — Credit capacity calculator (B)

**Why.** The AI credit decision (T9) must recommend a limit the policy allows. The numbers are
computed in code; the model only explains and decides within them.

**Policy (`Doc/knowledge/credit-policy.md` §3):**
- Assessed monthly income = the **lower** of the income evidenced by bank credits and by the
  filed ITR (§3.1.2). Bank side: `AaAnalysisResult.EstimatedSustainableInflow ÷ MonthsCovered`
  (the field is a total over the period, net of self-transfers and pass-through). ITR side:
  `MsmeFeatureSet.ItrLatestIncome ÷ 12` (gross total income for the year).
- Maximum FOIR by income: ≤ ₹50,000 → 45%; ₹50,001–2,00,000 → 55%; above → 65% (§3.1.3).
- Existing EMIs = those visible in Account Aggregator data, declared or not (§3.1.4):
  `MsmeFeatureSet.AaEmiMonthlyOutflow`.
- Room for a new EMI = max FOIR × income − existing EMIs. Eligible limit = the loan that EMI
  repays over the tenure at the rate (standard EMI formula, inverted).
- Above ₹50 lakh needs a tax audit report if the borrower is 44AB-liable (§5.3.2):
  `ItrAuditApplicable` / `ItrAuditCompleted`.

**Steps:** the contract is already in `FinRiskLensAI.Fraud/Credit/CreditCapacity.cs` (added
2026-10-16 so T9 could be built first): the `CreditCapacity` record, one field per figure with its
policy clause in a comment, and `CreditCapacityCalculator.Compute(facts, bank, requestedAmount,
tenureMonths, annualRatePct)`, which throws `NotImplementedException` until filled in. Fill in the
body only — T9 reads every field, and the Risk Lens shows them. `bank` is the customer's
`AaAnalysisResult`, null when no Account Aggregator data was shared (then bank income is zero, and so
is the assessed income). Rupee figures rounded to whole rupees; `PolicyClauses` lists what was
applied, e.g. `"§3.1.2"`. No Gemini, no database. Once it returns figures, **Generate credit
decision** on a Clear customer works end to end — nothing else to wire.

**Done when** unit tests cover each income band, a borrower with no room for a new EMI, and the
audit cap.

**✅ Done 2026-10-18.** Implemented in `CreditCapacityCalculator.Compute` (income bands, FOIR tiers 45%/55%/65%, annuity EMI and eligible amount, §5.3.2 tax audit cap, policy clauses). Covered by 6 unit tests in `CreditCapacityTests.cs`.


### T7 — Network links for businesses (A)

Add to `NetworkLens.Find` (keep the three existing links):

| Link | Data | Rule |
|---|---|---|
| **Same director** | `mca.json` → `message.details.directors[].din_number` (active directors) | One DIN on two or more customers |
| **Same mobile or email** | `MsmeEnquiry.MobileNumber`, `.Email` | Shared by separate customers |
| **Same address** | `MsmeEnquiry` flat / building / road / city / pin, normalised | Shared by separate customers |
| **Circular trading** | `gstr1_b2b_*.json` → `response.message.data.b2b[].ctin`, and each customer's own GSTIN | Customers invoicing each other in a loop (A→B→A, or A→B→C→A) |

`NetworkApplication` gains the customer's GSTIN, contacts, address, DINs and invoiced GSTINs.
Load them in `CaseRepository.GetNetworkAsync` from the database and the sources bucket.
`// ponytail:` note the ceiling — read per customer on each run; past a few hundred customers,
write them to an `application_entities` table when sources arrive.

**Done when** the circular-trading demo ring (T11) shows *Circular trading* and *Same director*,
and `DemoScenarioTests` still finds exactly the planted rings.

**✅ Done 2026-10-16.** `CustomerEntities` on `NetworkApplication`: GSTIN, mobile, email and address from
the registration (`MsmeEnquiry`), active directors from `mca.json`, own GSTIN and buyers from the
`gstr1_b2b_*` returns (read in `CaseService.ListRingsAsync`, cached per customer for 10 minutes and dropped
when a source arrives). `NetworkLens.BusinessLinks` adds *Same director*, *Same mobile*, *Same email*,
*Same address* (only when specific: a pin code and 20+ characters) and *Circular trading* (businesses that
reach each other through their invoices, any loop length). Each business counts once, by its latest
assessment, so it never links to itself. `NetworkLensTests` covers a three-business loop, a shared director
and mobile, an outsider who only sells into the loop, and one business's two assessments. On development:
still exactly the two planted rings; 2.2 s for the first ring computation, 0.3 s cached. The demo loop
itself comes with T11.

### T8 — Uploaded ITR vs the filed return (B)

An ITR the customer uploads is read by Gemini into fields (`assessment_year`,
`gross_total_income`, `business_turnover`, `total_income`, `tax_paid`). Compare them with the
filed return for the same assessment year from `itr.json` (`SourceFacts`). Any figure off by more
than ₹1 → `ITR_MISMATCH`, High, with `EvidenceKind.Field` evidence on the uploaded figures
(fields have ids and pages). Section: ITR (already mapped).

Needs an ITR PDF in the demo set: add `Render.Itr(...)` to `tools/FinRiskLensAI.DemoData/Render.cs`
(one page: name, PAN, assessment year, the five figures) and its truth fields.

**Done when** the demo customer with an edited ITR copy (T11) raises `ITR_MISMATCH`, and Gemini
reads the rendered ITR's five fields correctly in `eval`.

**✅ Done 2026-10-18** in `SourceChecks.ItrMismatch`, wired into `CaseService.RunChecksOnceAsync` and `DeterministicChecks.Run`. `Render.Itr` in `Render.cs` and `ItrDoc` in `Scenarios.cs`. Section: ITR (`RiskSections.Itr`). Covered by 9 unit tests in `SourceChecksTests.cs` (106/106 tests green).

### T9 — AI credit decision (A)

1. **Table** `credit_decisions` (DB_SCRIPTS entry first): id, loan_application_id, uan,
   requested_amount, product, tenure_months, rate_pct, capacity (jsonb, from T6), decision,
   recommended_amount, accepted_amount, reasons (jsonb), conditions (jsonb), model, created
   at/by, accepted at/by.
2. **Service** `FinRiskLensAI.Fraud/Ai/CreditDecisionService.cs`:
   - inputs: the customer's facts, the T6 capacity, the reviewed findings, and policy clauses from
     the existing search (`IKnowledgeService` — FOIR, escalation, gambling, documentation);
   - Gemini Pro, structured output: decision (Approve / Refer / Decline), product, recommended
     amount, tenure, conditions, reasons with citations;
   - **grounding:** every rupee figure must be one of the capacity figures or facts; the
     recommended amount can never exceed the eligible limit; citations must be retrieved clause
     ids. A reason that fails is dropped and shown; an invalid decision becomes *Refer*.
3. **Risk Lens panel** *Loan case*, shown only when the verdict is *Clear*: requested amount,
   product, tenure → **Generate decision** → the decision card (decision, limit, EMI, FOIR,
   reasons with citations) → the officer can lower the amount → **Accept**.

**Done when** the clear demo customer gets an *Approve* with a limit equal to or below the T6
figure and reasons citing §3.1.

**🔨 Built 2026-10-16 against the T6 contract; the live run waits on T6.** `ICreditDecisionService`
(Core) and `CreditDecisionService` in `FinRiskLensAI.Fraud/Credit/`. Decide: the verdict must be
*Clear* (`RiskSummary.Of`, the same rule as the Risk Lens) → facts and the AA analysis →
`CreditCapacityCalculator.Compute` (until T6 lands the officer sees "the credit capacity calculator
(T6) is not built yet") → five policy searches (FOIR, assessed income, EMIs on the statement,
escalation, tax audit) → Gemini Pro with the capacity and the extracts, falling back to 2.5 Pro →
`CreditDecisionService.Ground`: amount capped at `MaxLoanAmount`, every rupee figure in a reason or
condition must be a capacity figure or the recommended amount (±₹1), citations only to retrieved
chunks, an unknown outcome or *Approve* with nothing to lend or no reason left becomes *Refer*.
What it removes is stored in `dropped` and shown under the decision. Findings are not sent: a
*Clear* customer has none open. Table `credit_decisions` (migration `CreditDecisions`, on
development; production with the next deploy). Risk Lens *Loan case* panel (only when *Clear*):
product, amount, tenure, rate → **Generate** → outcome, the capacity figures, reasons with policy
sources, conditions, removed points → **Accept** at the recommended amount or lower (refused if the
customer is no longer *Clear*, a newer decision exists, or it is not an *Approve*).
`CreditDecisionTests` cover the cap, untraced figures, stripped citations and the *Refer* fallbacks.
Checked on development with a hand-made decision row (removed afterwards): the panel, accepting at
a lower amount, the gate before and after, and the T6 message on **Generate**; no overflow at phone
width.

### T10 — Gate and payload blocks (A gate · B payloads)

- **Gate (A):** `LoanCaseController.Preview` and `Push` refuse unless the latest assessment is
  *Clear* **and** has an accepted credit decision — checked on the server, not only hidden in
  the page.
- **Payloads (B):** `LoanCaseContext` (`ILoanCasePayloadBuilder.cs`) gains the risk summary and the
  accepted decision. Each builder adds:
  - `riskAssessment` — verdict, assessed at, reviewed findings (type, severity, review status)
  - `creditDecision` — decision, product, amount, tenure, EMI, FOIR, reasons, conditions, model,
    accepted by

  In the LOS, ULI (OCEN) and ONDC builders, in each one's own naming.

**Done when** the clear customer's preview shows both blocks on all three channels and a risky
customer's push is refused.

**Gate ✅ 2026-10-16.** `ICreditDecisionService.PushBlockedAsync` — no assessment, a verdict other than
*Clear*, no decision, the latest not an *Approve*, or not accepted each return the reason, which
`Preview` shows as its error page and `Push` returns as its message. For the payloads: read the
accepted decision with `ICreditDecisionService.GetLatestAsync(uan)` (its `Capacity`, `Reasons` and
`Conditions` are JSON, camelCase) and the verdict with `ICaseService.GetByUanAsync` + `RiskSummary.Of`.
Fixed 2026-10-17: the verdict counted only *open* findings, so confirming a High finding turned the customer
*Clear* and opened the loan case. Now every finding that is not dismissed counts (`RiskSummary.Of`, the
Customers list and the Risk Lens sections); a test covers confirmed vs dismissed.

**Payloads ✅ Done 2026-10-18.** `LoanCaseContext` populated with `RiskAssessment` and `CreditDecision` in `LoanCaseService.BuildContextAsync`. `riskAssessment` and `creditDecision` blocks appended across all three payload builders (`LosPayloadBuilder.cs`, `UliPayloadBuilder.cs`, and `OndcPayloadBuilder.cs`). Verified by unit tests in `LoanCasePayloadTests.cs`.

### T11 — Demo customers (B builds · A seeds production)

Six synthetic businesses, each a registered customer with source data and uploads
(`12_CUSTOMER_RISK_LENS.md` §7):

| Customer | Udyam | Story | Expected |
|---|---|---|---|
| Clear | `UDYAM-MH-99-0000301` | Consistent everywhere | *Clear* → Approve |
| Edited statement | `…0302` | Upload disagrees with the AA record | *High risk* — `STATEMENT_VS_AA_MISMATCH` |
| Turnover inflation | `…0303` | Bank credits ≈ 3× GST and ITR | *High risk* — `TURNOVER_MISMATCH` |
| Edited ITR | `…0304` | Uploaded ITR turnover higher than filed | *High risk* — `ITR_MISMATCH` |
| Mule-like account | `…0305` | AA: every receipt forwarded the same day | *High risk* — `AA_PASS_THROUGH` |
| Circular traders | `…0306–0308` | Invoice each other, one shared director | *High risk* — `FRAUD_RING` |

**Steps (B):** a `customers` command in `tools/FinRiskLensAI.DemoData`:
1. **Registration:** reuse the onboarding service to save the Udyam record and the user
   registration, so each appears on **Customers** and can sign in.
2. **Sources** into `gs://finrisklensai-sources/UDYAM-MH-99-*`: `udyam.json`, `aa.json`
   (`AaFeed` in `Scenarios.cs` — make it public), `gst_taxpayer.json` and GST returns, `itr.json`, `mca.json` with directors —
   generated from the dummy-data service with the story's numbers.
3. **Uploads** through `CaseService.AddCustomerDocumentsAsync`, with the truth applied in place of
   Gemini (like `seed`).
4. Print expected vs actual verdict and findings per customer; exit 1 on any mismatch.

A runs it against production through the Cloud SQL Auth Proxy (`DB_SCRIPTS.md`).

**Status: ✅ Done 2026-10-18.** Implemented `customers [--export-dir <dir>]` CLI command in `tools/FinRiskLensAI.DemoData/DemoCustomers.cs`. Generates structured JSON source files customer-wise (`udyam.json`, `gst_taxpayer.json`, `gstr3b_*.json`, `gstr1_b2b_*.json`, `itr.json`, `mca.json`, `DIN_*.json`, `aa.json`) into `gs://finrisklensai-sources/{UAN}/` and local directory. Seeds registration in DB (`MsmeEnquiries`, `UserRegistration`, `UserOtpModel` with OTP `123456`) and runs case checks with exact expected findings matching all 8 entities (6 business scenarios). Idempotent across re-runs.

### T12 — Measurements (A)

`eval` on the new demo documents (statements, ITR copies) and `investigate` on the demo
customers; update *Measured, not assumed* in the README and §7 of the build plan.

### T13 — README and architecture (B)

Rewrite the README's *Try it*, demo table and pipeline for the customer flow. Update the ✅/🆕 marks
in `ARCHITECTURE.md` §1. Keep every number traceable to a command (T12).

### T14 — Deck and video (A deck · B records)

- **Deck:** `Doc/FinRiskLens_AI_Builder_Cup_2026.pptx` is built by a script — A regenerates it with
  Claude. Changes: the process-flow slide (customer → Risk Lens → loan case → LOS / ULI / ONDC), the
  wireframe, new snapshots, the demo table (customers), and the credit decision.
  **🔨 Deck updated 2026-10-17:** team name *AS FinTech Developer*; every slide to the customer flow (share →
  Risk Lens → AI credit decision → LOS / ULI / ONDC), 70 tests, business network links, AA risk flags; new
  snapshots from development (home, Customers, the Risk Lens and loan case of a Clear customer, statement vs
  AA, AI investigator); slide 17 lists the T11 demo customers.
  **Updated 2026-10-17 night (21 slides):** slide 1 is A's own edit (team leader *Abhinav Mukwane & Sourabh
  Thakre*) and is kept as is — the build inserts it from the saved deck rather than regenerating it.
  Seven screenshot slides: home and customer dashboard; Business Registration (Udyam), Banking Data (AA)
  overview and risk indicators, GST, ITR and MCA; the bank dashboard and customer page; Customers, the Risk
  Lens, a finding with its evidence, and a Clear customer's loan case — on Reddy Industries (sandbox AA data,
  *High risk*) and Sankalp (*Clear*); the viewer's IP, the V1 bank's name and a mobile number are blurred.
  The demo-customer list became **Try it on the live site** (customer and bank walkthrough). **Still to do
  after T6:** retake the loan-case screenshot with a real credit decision.
- **Video:** rewrite `Doc/VIDEO_SCRIPT.md` for the customer flow (customer uploads → Risk Lens → a
  risky customer → the clear customer's loan case and decision → push to LOS); record it on the live
  site after T15's deploy; upload; put the link on the deck's links slide.

### T15 — Deploy and submit (A)

1. Merge everything; tests green.
2. Apply the pending migrations to production through the proxy: `BankDataAt` (T1) and the
   `credit_decisions` table (T9).
3. Deploy with the command in `10_FRAUD_LENS_BUILD.md` → *Deploying*.
4. **Smoke test** on <https://finrisklensai.cloud.run>: home page, bank sign-in, Customers verdicts,
   each demo customer's Risk Lens, the clear customer's decision and a simulated LOS push,
   the customer dashboard upload.
5. Submit the deck (with the video link), repository and product link on the Cup portal.

---

## 6. If time runs short — cut in this order

1. **T7 circular trading** (keep shared director and contacts)
2. **T8** uploaded ITR vs filed
3. **T4** name normalising (keep PAN, GSTIN and company status)
4. **ONDC** decision block (keep LOS and ULI)

**Never cut:** T1, the gate (T10), the grounding of the credit decision (T9), evidence on every
finding, and the smoke test.

## 7. Daily check-in

Fifteen minutes each morning and evening: what moved on the board, what is blocked, what the next
pull request is. Anything blocked for more than an hour goes to the other person at once.

---

## 8. Content and UI copy (B)

The rules and every approved wording live in [`CONTENT_GUIDELINES.md`](../CONTENT_GUIDELINES.md) at
the repository root, the single source of truth for text users see. Read it before changing a page.
Content changes go through a pull request that A merges (§4); on Oct 16 and 17 they went to `main`
directly.

| Page | Status |
|---|---|
| Onboarding, Sign In, OTP | ✅ C1 |
| Home page | ✅ C2 |
| Customer dashboard and sidebar | ✅ C3 — international terminology (e.g. *Tax & Sales Compliance (GST)*); the GST card always reads *Retrieved*, because the GST page shows its fixed dataset either way |
| Udyam page | ✅ C3 — *AI-Powered Business Registration Analysis*; GSTIN and PAN shown as listed on Udyam, not as checked |
| GST page | ✅ C4 — one fixed sample dataset at the top of `_GstAnalysisBody.cshtml`, the same for every business, until a live GST integration exists. No on-page sample label (B's decision, Oct 17), so the README, the deck and the video say it instead |
| ITR page | ✅ C4 — the summary claims no more than the return shows |
| MCA page | ✅ C5 — director (DIN) details in the approved format; **OFAC and AML board screening in demo mode** (`McaController.ScreenDirector`): the director's name compared with a short fictional list, every result labelled *Illustrative Demo — Not a Live OFAC/AML Check* |
| Account Aggregator page | ⏸ C6 |
| Risk Lens and the bank pages | ⏸ C6, with A — they show the findings |
| Home page Product Guide | 👀 C7 — public assistant (`ProductGuideController`), separate from the signed-in assistant: product knowledge only, never customer data |

**Open for A** (from B's notes): the browser title (`_Layout.cshtml`) still reads *Evidence-based
fraud detection* — change it only if we agree to; the ITR modal on the dashboard (and the GST modal,
no longer reachable) asks for a portal password that is never sent to the server — wire it to a real
portal flow or remove the fields.
