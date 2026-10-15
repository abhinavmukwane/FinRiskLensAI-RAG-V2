# 12 — Customer Risk Lens: one assessment per customer

**Decided 2026-10-15.** FinRiskLens moves from *cases* to *customers*. The bank no longer opens
a case and uploads files. The **customer** (an MSME, identified by its Udyam number) shares its
data — fetched from the source or uploaded — and the bank sees **one fraud and risk page per
customer**, with every source combined and analysed section by section. When nothing is wrong,
the bank raises the **loan case** with an **AI credit decision** and sends it to its **LOS**, to
**ULI (OCEN)** or to **ONDC**.

This document is the plan. What is built is tracked in
[`10_FRAUD_LENS_BUILD.md`](10_FRAUD_LENS_BUILD.md) (Phase 7); the system design is in
[`ARCHITECTURE.md`](ARCHITECTURE.md).

---

## 1. The flow in one picture

```
 CUSTOMER (MSME)                                   BANK
 ───────────────                                   ────
 ① Register with the Udyam number
 ② Share data — fetch or upload ──────────►  ③ Customers list: risk status per customer
    • Bank statements via Account Aggregator     ④ Customer page — Risk Lens
    • GST · ITR · MCA                               verdict + one section per source
    • Upload: statements, ITR, GST, financials   ⑤ Review findings: confirm or dismiss
                                                  ⑥ No open risk? → Generate loan case
                                                     AI credit decision (approve / refer / decline,
                                                     limit, product, reasons with evidence)
                                                  ⑦ Send to LOS · ULI (OCEN) · ONDC → receipt
```

## 2. Customer journey

| Step | What the customer does | Status |
|---|---|---|
| 1. Register | Enters the Udyam number, checks the details, consents, verifies by email OTP | ✅ built |
| 2. Fetch data | One button per source on the dashboard: **Account Aggregator** (consent in the AA app — live Finvu sandbox), **GST**, **ITR**, **MCA** | ✅ built (GST, ITR, MCA are sample data in the demo; live sources need licensed providers) |
| 3. Upload documents | A new **Upload documents** card: bank statements of accounts not shared through AA, ITR copies, GST returns, audited financials. PDF or photo | ✅ built |
| 4. See progress | Dashboard shows which sources are shared and that the bank is reviewing. Risk details stay bank-side | ✅ each upload: Reading / Received / could not be read |

Every upload and every fetched source lands in the customer's own folder
(`gs://finrisklensai-sources/{UDYAM}/` for source data, `gs://finrisklensai-cases/` for uploads)
and starts a fresh assessment automatically.

## 3. Bank journey

| Step | What the bank officer sees and does | Status |
|---|---|---|
| 1. Customers | The list gains a **risk status** per customer — *Clear*, *Review*, *High risk*, *Awaiting data* — and data completeness | ✅ risk column |
| 2. Risk Lens | The customer page opens on a new **Risk Lens** tab: the verdict, then one section per source (below). The existing Udyam, GST, AA, ITR and MCA tabs stay as the detail views | ✅ built |
| 3. Review | Each finding has its evidence links and **Confirm / Dismiss** with a note | ✅ reuse |
| 4. Loan case | **Generate loan case** is enabled only when no High or Medium finding is open. A confirmed finding blocks it; a dismissed one does not | 🆕 gate |
| 5. AI credit decision | Gemini Pro recommends **Approve / Refer / Decline**, the product, limit and tenure, and the reasons — every amount traced to the customer's own data, every policy point cited. The officer can adjust the amount before accepting | 🆕 to build |
| 6. Send | Preview the payload, confirm, get a receipt — **LOS**, **ULI (OCEN 4.0)**, **ONDC (beckn)**. The payload now carries the risk assessment and the credit decision | ✅ push exists · 🆕 two new blocks |
| 7. Rings | The Rings page lists **customers** linked to each other | ✅ reuse, links extended |

The **Cases** menu and **New case** form leave the bank portal: the bank works per customer.

## 4. Risk Lens — the sections

Each section shows its own analysis and a status — **Pass**, **Watch** or **Flag** — with the
findings behind it.

| Section | Analysis | Built from |
|---|---|---|
| **Verdict** | Overall status, open findings by severity, data completeness, last assessed | All sections |
| **Identity** (Udyam + MCA) | Name, PAN, GSTIN and address agree across Udyam, GST, ITR and MCA; company active; directors verified | 🆕 consistency checks over existing source data |
| **GST** | Turnover trend, filing regularity, turnover against ITR and against bank credits | ✅ GST analysis + 🆕 cross-source finding |
| **Bank (Account Aggregator)** | The bank's own transactions: cash flow, EMIs, bounces, round-tripping, pass-through, window dressing | ✅ AA deep analysis → 🆕 its risk flags become findings |
| **ITR** | Filed turnover and income; an uploaded ITR copy against the filed return | ✅ ITR analysis + 🆕 uploaded-vs-filed check |
| **Uploaded documents** | Every upload read by Gemini: arithmetic, file history, and statements against the bank's AA record | ✅ nine checks, bank-record cross-check |
| **Network** | Links to other customers: same file or template, same salary or payment account, **shared director (DIN)**, **shared mobile, email or address**, **circular trading** (customers invoicing each other in GST) | ✅ file, template, account · 🆕 MSME links |
| **AI investigator** | Gemini Pro reads all of the above with tools bound to this customer; every claim cites rows or facts | ✅ reuse, scoped per customer |

## 5. AI credit decision

| | |
|---|---|
| **When** | Only when the verdict is *Clear* (no open High or Medium finding) |
| **Model** | Gemini Pro, structured output |
| **Inputs** | The customer's verified facts (turnover, cash flow, obligations, filing history, company status), the reviewed findings, the requested amount, and the credit policy through the existing policy search |
| **Output** | Decision (Approve / Refer / Decline) · product (working capital, term loan) · recommended limit and tenure · conditions · reasons |
| **Grounding** | The existing verifier: every rupee amount must trace to the customer's facts, every policy point to a retrieved clause — or it is dropped and shown |
| **Officer** | Can adjust the limit and add a note before accepting; the decision, its version and who accepted it are stored |
| **Stored** | New table `credit_decisions` (entry in `DB_SCRIPTS.md` first) |
| **Sent** | A `creditDecision` and a `riskAssessment` block in the LOS, ULI and ONDC payloads |

## 6. Changes to the code

| Area | Change |
|---|---|
| Data | `applications` becomes the customer's **assessment**: one per Udyam number (unique index), documents attached by upload source (customer or bank). `credit_decisions` table |
| Upload | `CaseService.CreateAsync` → `AddDocumentsAsync(uan, files, uploadedBy)` from the customer dashboard |
| Pipeline | Unchanged steps; `RunAssessmentAsync(uan)` starts on every upload and every fetched source |
| Checks | Account Aggregator risk flags → findings; GST–ITR–bank turnover reconciliation; identity consistency; uploaded ITR vs filed ITR |
| Network lens | New links: shared director DIN, shared mobile / email / address from registration, circular GST trading among customers |
| Investigator | Tools scoped to the customer instead of the case |
| Credit decision | New `CreditDecisionService` (Gemini Pro + grounding) |
| Loan case | Gate on the verdict; `riskAssessment` and `creditDecision` blocks in the three payload builders |
| Bank UI | Risk status on **Customers**; **Risk Lens** tab first on the customer page; **Cases** menu removed; **Rings** lists customers |
| Customer UI | **Upload documents** card and status on the dashboard |
| Demo data | Demo **customers** instead of demo cases (below) |
| Docs, deck, video | README, architecture, deck and video script rewritten to the customer flow |

## 7. Demo customers

Each a synthetic MSME (`UDYAM-MH-99-*`) with a registered account, source data and uploads:

| Customer | Story | Expected |
|---|---|---|
| Clear | Consistent everywhere | *Clear* → loan case → **Approve**, limit from cash flow |
| Edited statement | Upload disagrees with the bank's AA record (today's DEMO-T04) | *High risk* — bank-record finding |
| Turnover inflation | Bank credits far above GST and ITR turnover | *Review* — cross-source finding |
| Hidden EMIs | AA shows EMIs to lenders the application never declared | *Review* — AI investigator |
| Mule-like account | AA shows every receipt forwarded the same day | *High risk* — pass-through |
| Circular trading ring | Three customers invoice each other, share a director | *High risk* for all three — ring |

## 8. Plan and timing

Submission deadline **Oct 18** (judging Oct 19 – Nov 6). The scope moves the target from
Oct 16 to **Oct 18**.

| Day | Work | Done when |
|---|---|---|
| **Oct 16** | Assessment per customer; customer upload card; Risk Lens tab with sections from today's findings; Cases menu out; risk status on Customers | A customer upload produces findings on that customer's Risk Lens |
| **Oct 16–17** | AA risk flags as findings; turnover reconciliation; identity consistency; MSME network links | Each demo customer shows its expected section status |
| **Oct 17** | AI credit decision, the gate, payload blocks | The *Clear* customer reaches a pushed loan case with a decision; the others cannot |
| **Oct 17–18** | Demo customers in both databases; eval; README, deck, video re-recorded; deploy; **submit** | Live site walks the whole flow |

**Cut first if time runs short:** circular-trading link → identity consistency (keep company
status only) → ONDC decision block. **Never cut:** the gate, the grounding of the credit decision,
evidence links on every finding.

## 9. Decisions taken by default (change any of these)

| Question | Default |
|---|---|
| Can the bank still upload for a customer? | No — the customer uploads; a branch can help the customer do it |
| What blocks the loan case? | Any **open** High or Medium finding; confirmed findings block, dismissed do not |
| Can the officer override a Decline? | No — Refer goes to a senior officer outside the system; the officer can lower a recommended limit, not raise it |
| Does the customer see findings? | No — they see their data and that the bank is reviewing |
| Existing demo cases | Replaced by demo customers; the case pages go with the Cases menu |
