# Mentor–Mentee Connect — Session Brief

**Team ASM FinTech · FinRiskLens AI · IDBI Innovate 2026**
**Format: 10 minutes — 5 min explanation, 5 min guidance & feedback**

---

## Before you start

- Have the app **already open and logged in** in two tabs: the MSME dashboard and `/BankAdmin/Dashboard`. Do not spend session time on login screens.
- **One person speaks** for the 5-minute explanation. A second person drives the screen.
- Keep a notepad open — you will get more value from writing their answers down than from finishing every slide.
- If they interrupt with questions during the explanation, **let them**. Their questions are the actual product of this session.

**15-second version** (if they ask "what does it do?" up front):

> We score MSMEs that have no credit history, using the data they already generate — Udyam, GST, ITR, bank statements via Account Aggregator, and MCA — and turn it into a 1000-point Financial Health Score with an explainable breakdown and an indicative lending limit, delivered to the branch officer in a bank portal.

---

# PART 1 — The 5-minute explanation

> Timings are cumulative. Practise once; it is tighter than it looks.

### 0:00 – 0:45 · The problem

- India has roughly **6.3 crore MSMEs**; a large share are **New-to-Credit or New-to-Bank** — no bureau record, no audited financials, so a traditional scorecard has nothing to work with.
- The result is not that they get bad terms — it is that the file **cannot be assessed at all**, so it is declined or never opened.
- But these businesses **do** generate data: Udyam registration, GST returns, ITR filings, bank transactions, MCA filings. It is just scattered across systems and not in a form a credit officer can act on.

**Say this line:** *"Our premise is simple — the data to underwrite these borrowers already exists. It just isn't assembled."*

### 0:45 – 2:00 · What we built

Two connected surfaces:

**1. MSME side** — the borrower onboards with **only their Udyam Registration Number**. From that one input we pull and assemble:

| Source | What it gives |
|---|---|
| Udyam | Verified identity, vintage, NIC sector, linked PAN/GSTIN |
| PAN | Identity cross-check |
| GST — taxpayer, GSTR-1, 2A, 2B, 3B | Turnover trend, filing discipline, ITC behaviour, customer concentration |
| ITR | Declared income trend, P&L and balance-sheet ratios |
| Account Aggregator (Finvu) | Consent-based bank statements — inflows, EMI load, cheque/NACH returns |
| MCA / DIN / CIN | Company status, paid-up capital, directors, **registered charges** |
| IP risk | Session-origin fraud check |

**2. Bank side** — a portal for the branch officer: portfolio dashboard, searchable customer list, and a **Customer 360** with everything the bank needs on one screen.

### 2:00 – 3:30 · Demo — follow this exact path

> Three clicks. Do not wander.

1. **Bank Dashboard** — *"Portfolio view: how many onboarded, how many scored, how many are lendable today, how many are flagged at-risk. Plus band distribution and onboarding trend."*
2. **Customers → click a row** — *"Search and filter by score band, state, or analysis status."*
3. **Customer 360 — walk the five tabs:**
   - **Financial Score** — the gauge, the six-dimension breakdown, the lending assessment, and the recommended credit product
   - **Udyam Identity** — business identity score, vintage, plant locations, NIC activities
   - **GST Analysis** — filed returns, turnover pattern
   - **Corporate (MCA)** — company status, directors, and **open charges — existing secured borrowing the bank would otherwise miss**
   - **Security Audit** — source-IP fraud check

**Land this point:** *"Every number here is traceable to a source document. Nothing is a black box."*

### 3:30 – 4:30 · What makes it a credit tool, not a dashboard

Four things worth saying explicitly to a banker:

1. **Six dimensions, transparently weighted** — Revenue Vitality 250, Cash Flow Health 200, Transaction Trustworthiness 150, Compliance Quotient 150, Business Stability 150, Debt Serviceability 100. Total 1000. Bands: **800+ Excellent, 650+ Good, 500+ Fair, 350+ At Risk, below 350 High Risk.**
2. **Hybrid, not pure ML** — a transparent rules engine and a LightGBM model, blended **60% rules / 40% ML**. The rules half means we can always explain a score to a customer or an auditor, which matters under RBI digital-lending norms.
3. **Missing data degrades gracefully** — if AA consent isn't given or there's no GST registration, we **redistribute the weight** rather than zeroing the dimension, and we **disclose on the card** that the score was computed on reduced inputs. We never fabricate a number.
4. **It outputs a decision, not just a score** — indicative working-capital limit via the **Nayak turnover method** (20% of annual turnover, 5% borrower margin), term-loan capacity from EMI headroom annuitised over 5 years at 11%, standard ratios (DSCR, FOIR, current ratio, debtor days), and a **recommended product** — Standard WC / Term Loan for good files, **CGTMSE-backed** for moderate, **MUDRA** for entry-tier.

Also worth a sentence if time allows: **cross-source anomaly detection** — we compare GST turnover, ITR income and bank credits against each other and flag material divergence as a data-integrity signal, kept separate from creditworthiness.

### 4:30 – 5:00 · Where we are, and the ask

**Built and working today:** end-to-end MSME onboarding, the scoring engine, the full Financial Health Card, Udyam / GST / MCA / DIN / IP screens, the AA consent journey, a downloadable report, the bank portal, and multilingual UI via **Bhashini (7 languages)**.

**Running on:** cached and synthetic responses in the documented API shapes — we have already submitted our **Data Field Requirements** document covering all 17 APIs.

**Pending:** sandbox API access, EPFO, and the outbound ULI/OCEN-compliant score API.

**Close with:** *"We've built the assessment engine. What we need from you is direction on how a bank actually wants to consume it — and that's what we'd like to use the next five minutes for."*

---

# PART 2 — Questions to ask

> You will realistically get through **3–5**. Ask the starred ones first — their answers change what we build next.

## ⭐ Ask these three first

**1. Where does this sit in IDBI's process — and who reads the score?**
> *"Is this a pre-sanction tool that qualifies leads before a file is opened, or does it sit inside the credit appraisal workflow as an input to the officer's assessment? And who is the intended consumer — the branch officer, the central credit team, or an automated decisioning layer?"*

*Why it matters:* determines whether we optimise for speed-and-triage or for depth-and-audit-trail. Completely different products.

**2. What shape should the integration take?**
> *"Should this be a standalone portal your officers log into, an API that feeds your existing LOS, or a module embedded in your current branch interface? If it's an API — which LOS and CBS are you running, and what's your standard integration pattern?"*

*Why it matters:* we've built a portal. If IDBI wants an LOS integration, we need to prioritise the outbound API instead.

**3. What would make an alternate-data score acceptable to your credit policy team?**
> *"What score cut-offs would map to auto-approve, refer-to-officer and decline in your process? And what would it take for a score like this to be used alongside — or instead of — your existing MSME scorecard?"*

*Why it matters:* the single biggest gap between a hackathon prototype and something a bank can actually act on.

## Integration & technical

4. **ULI / OCEN** — *"Should our outbound score API conform to the ULI DSP specification, or does IDBI have an internal API standard we should target instead?"*

5. **Deployment & data residency** — *"Would this run on-premise, in IDBI's private cloud, or as a hosted service? Any constraints on where MSME data can be stored, and what retention period applies?"*

6. **Authentication** — *"For bank-side access, would you expect integration with IDBI's AD/SSO rather than our own user table?"*

7. **Data refresh** — *"How often would you want a score refreshed — on-demand at application, monthly, or event-driven on new GST filings?"*

## Scoring & credit judgement

8. **Weighting** — *"Our six dimensions are weighted 25/20/15/15/15/10. Does that broadly match how your credit policy weighs MSME risk, or would you shift it — for example, more weight on banking behaviour and less on compliance?"*

9. **Thin-file threshold** — *"For an MSME with no bureau record and no AA consent, what's the minimum data set IDBI would still consider assessable? Where do you draw the line between 'thin file' and 'not assessable'?"*

10. **Charges and existing exposure** — *"We surface MCA registered charges as an indicator of existing secured borrowing. How much weight does your team give that in practice, and is there a bureau or CERSAI feed you'd expect us to reconcile against?"*

11. **Sector risk** — *"We apply a static NIC-based sector risk weight. Does IDBI maintain an internal industry risk rating we should align to instead?"*

## Process & next steps

12. **Sandbox access** — *"When does the sandbox open, and which of the 17 APIs in our requirements document will actually be available? Are any of them out of scope for the prototype phase?"*

13. **Evaluation** — *"What does the final evaluation look like — a live demo, a code review, a business case, or all three? What weighs most?"*

14. **Pilot path** — *"If this progresses past the hackathon, what would a POC look like — which branch or segment, how many files, and over what period?"*

15. **Compliance** — *"Is there a compliance or model-governance review we should anticipate? Anything under the RBI Digital Lending Guidelines we should build in now rather than retrofit — consent artefacts, audit trails, model explainability documentation?"*

---

## Closing (last 20 seconds)

> *"Thank you — this was genuinely useful. Two things: could we send follow-up questions if they come up as we build? And is there anything specific you'd like to see working by the next checkpoint?"*

That last question is worth asking every time. It converts vague feedback into a concrete deliverable.

---

## Quick-reference sheet

Keep this visible during the call in case they ask for a specific number.

| | |
|---|---|
| Score range / bands | 0–1000 · Excellent 800+ · Good 650+ · Fair 500+ · At Risk 350+ · High Risk <350 |
| Dimensions | Revenue 250 · Cash Flow 200 · Transactions 150 · Compliance 150 · Stability 150 · Debt Service 100 |
| Model blend | 60% transparent rules engine + 40% LightGBM calibration |
| Data sources | 17 APIs — Udyam, PAN, GST ×7, ITR, AA ×3, MCA, CIN, DIN, IP, EPFO |
| Data window | GST & AA trailing 12 months · ITR trailing 3 assessment years |
| WC limit method | Nayak turnover method — 20% of annual turnover, 5% borrower margin, band-adjusted |
| Term loan | EMI headroom annuitised, 5 years @ 11% p.a., FOIR-capped |
| Products | Standard WC / Term Loan → CGTMSE → MUDRA, by band |
| Storage | Raw responses per MSME in Azure Blob (folder = UAN) + SQL score index |
| Languages | 7 via Bhashini — EN, HI, MR, TA, GU, KOK, SA |
| Calls per onboarding | ~50–60, dominated by the 12-month GST pull |

---

## Things to avoid saying

- **Don't oversell readiness.** Say "running on synthetic data in the documented API shapes, pending sandbox access." Bank teams respect precision and will find the gap anyway.
- **Don't call it a credit decision.** It is an *indicative assessment* and a *recommendation*. The sanction is always the bank's.
- **Don't promise a timeline** you haven't checked with the full team.
- **Don't argue if they criticise the weighting or the approach.** Write it down and ask a follow-up — that feedback is exactly what you came for.
