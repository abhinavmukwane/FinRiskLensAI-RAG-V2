# Finals Demo Script — 3 Speakers

**IDBI Innovate 2026 · Grand Finale · Team ASM FinTech Developer**
**Audience: IDBI Bank credit, technology and innovation teams**

**Runs 12 minutes + Q&A.** A trim path down to 8 minutes is at the end.

Companion docs: [`DEMO_ACCOUNTS.md`](DEMO_ACCOUNTS.md) (the five accounts and their
numbers), [`MENTOR_SESSION_BRIEF.md`](MENTOR_SESSION_BRIEF.md) (deeper answers and the
quick-reference sheet), [`09_FINALS_ENHANCEMENT_PLAN.md`](09_FINALS_ENHANCEMENT_PLAN.md)
(what's next).

---

## Who speaks when

Names are a suggestion — swap them however you like, but **rehearse in the final
order** and don't change it on the day.

| | Speaker | Section | Time | Screen |
|---|---|---|---|---|
| **1** | **Abhinav** | The problem & what we built | 0:00 – 2:00 | Slides |
| **2** | **Mayur** | The MSME journey — live | 2:00 – 6:00 | App, customer side |
| **3** | **Thakre** | The banker's view — live | 6:00 – 10:30 | App, bank portal |
| — | *(Thakre)* | *Optional:* the loan-case hand-off | +45 s | Customer 360 → push modal |
| **4** | **Abhinav** | Where we are & the ask | 10:30 – 12:00 | Slides |
| — | **All three** | Q&A | — | Hold on Customer 360 |

**One person drives the mouse for the whole demo** — whoever is *not* speaking in
sections 2 and 3 should not touch anything. Decide now who drives. The speaker
speaks; the driver clicks. Nothing looks worse than two people reaching for the
laptop.

---

## Pre-flight — do this 15 minutes before

Tick every line. Most demo failures are setup failures.

- [ ] App running and reachable. Check **UAT SQL is up** — the demo accounts live there
- [ ] **Tab 1:** logged in as `shreeji.precision@finrisklens.demo`, sitting on the **Dashboard**
- [ ] **Tab 2:** logged in at `/Auth/BankLogin` as `bankadmin`, sitting on **Customers**
- [ ] **Tab 3:** `/Onboarding/CustOnboarding` — the blank Udyam entry screen
- [ ] Browser zoom **110–125%**. Bankers are reading your screen from a projector
- [ ] Close every other tab, Slack, mail, notifications. Do Not Disturb on
- [ ] Theme set to **IDBI palette** (theme 1) before you start, not during
- [ ] Phone hotspot ready as backup internet
- [ ] Slides open in a separate window, already on slide 1
- [ ] **Know these five numbers cold:** 813 · 806 · 715 · 627 · 376
- [ ] *If you plan to run the optional loan-case beat:* decide whether Sankalp should
      start clean (raise the case live) or already pushed (show the chips). Sankalp
      currently has all three channels pushed — clearing SQL is in
      `DEMO_ACCOUNTS.md` → "Loan-case pushes"

### The one hard rule

> **Only demo the MCA / Corporate tab on Sankalp Autotech (806) or UCN Fibrenet (715).**
> The other company records in the portal still carry generator defects — wrong
> director PANs, capital and AGM dates that don't make sense. Do not open MCA on
> anything else in front of a banker.

---

# 1 · Abhinav — The problem & what we built
### 0:00 – 2:00 · Slides

### 0:00 – 0:45 · The problem

> "Good morning. We are team ASM FinTech Developer, and our problem statement is
> the Financial Health Score.
>
> India has about **six point four crore MSMEs**. A large share of them are
> New-to-Credit or New-to-Bank — no bureau record, no audited financials. So when
> a file reaches a credit officer, there is nothing to assess. It isn't that these
> businesses get worse terms. The file simply **cannot be assessed**, so it gets
> declined or never opened at all.
>
> But these businesses do generate data. Udyam registration. GST returns. Income
> tax filings. Bank transactions. MCA filings.

**Land this line, slowly:**

> **"Our premise is simple — the data to underwrite these borrowers already
> exists. It just isn't assembled."**

### 0:45 – 1:30 · What we built

> "So we built two connected surfaces.
>
> On the **MSME side**, the business onboards with one input — their Udyam number
> — and we assemble five sources behind their consent: Udyam, GST, income tax,
> bank statements through the Account Aggregator framework, and MCA company and
> director records.
>
> On the **bank side**, a portal for your officer: a portfolio dashboard, a
> searchable customer list, and a Customer 360 with everything on one screen.
>
> In between sits the scoring engine. Six dimensions, a thousand points. Sixty per
> cent a transparent rules engine, forty per cent a LightGBM model. The rules half
> means we can always explain a score — to a customer, or to an auditor."

### 1:30 – 2:00 · Set up the demo

> "Three things we'd like you to watch for.
>
> **One** — every number on screen traces back to a source document. Nothing is a
> black box.
>
> **Two** — when data is missing, we don't fabricate and we don't zero it out. We
> redistribute the weight and we say so on the card.
>
> **Three** — this isn't only origination. Every time we score a business, we keep
> it, so you get the trend. That's the monitoring problem, not just the
> application problem.
>
> Mayur will take you through what the business owner sees."

**→ HANDOVER.** Driver switches to Tab 3 (blank Udyam screen).

---

# 2 · Mayur — The MSME journey
### 2:00 – 6:00 · Live app, customer side

> ⏱ **Watch the clock here.** This section runs long in rehearsal every time.

### 2:00 – 2:45 · Onboarding — do this live

**[Tab 3 · blank Udyam entry]**

> "Onboarding starts with one number. Nothing else."

**Type a Udyam number. Click Fetch & Continue.**

> "That pulls the enterprise record — legal name, constitution, date of
> incorporation, GSTIN, PAN, and the registered address with a geotag. The owner
> hasn't typed any of it.
>
> They check it, tick consent, and register. Identity is confirmed with an email
> OTP.
>
> And notice the language selector — the whole platform runs in **seven Indian
> languages** through Bhashini. If we're serious about a branch in Nanded, not
> just Mumbai, that isn't decoration."

**Open the language dropdown, switch to Hindi or Marathi, switch back.**

### 2:45 – 3:00 · The honest switch

> "I'm going to switch to an account that's already onboarded, so we're not
> watching a twelve-month GST pull run live."

**→ Switch to Tab 1 (Shreeji dashboard).** Say it out loud — don't pretend it's
the same session. Bankers notice, and admitting it costs you nothing.

### 3:00 – 3:50 · Collecting the data

**[Tab 1 · Dashboard — the four fetch cards]**

> "From here the business connects each source, one button each.
>
> **GST** — twelve months, seven return types. GSTR-1, 2A, 2B, 3B, HSN summary,
> credit notes. That's turnover, filing discipline, input tax credit behaviour and
> customer concentration.
>
> **Account Aggregator** — this one leaves our platform. The owner approves on the
> AA's own screen, under the RBI framework, and we receive the statement. We never
> see their banking credentials.
>
> **Income tax** — declared income, tax paid, whether the audit was done.
>
> **MCA** — and this one only appears for companies. A proprietorship has no CIN
> and no directors, so the platform doesn't pretend otherwise.
>
> Once a source is fetched, the button locks. No accidental double pulls."

### 3:50 – 5:15 · The Financial Health Card

**[Click Financial Score]**

> "This is the Financial Health Card.
>
> **Eight hundred and thirteen out of a thousand.** Excellent band."

**Point at the gauge, then move down to the six dimensions.**

> "Six dimensions, each with its own points and maximum. Revenue Vitality
> one-eighty-six of two-fifty. Cash Flow Health one-sixty-nine of two hundred.
> Transaction Trustworthiness one-forty-three of one-fifty. Compliance
> one-forty-seven of one-fifty. Business Stability one-thirty-six of one-fifty.
> Debt Serviceability seventy-eight of a hundred.
>
> And every one carries a **reason** — twelve of twelve GST returns filed, zero
> cheque bounces, EMI at three per cent of inflow. Not a number with no story
> behind it."

**Scroll to the lending assessment.**

> "Then it stops being an analytics screen and becomes a credit input.
>
> Annual turnover **seven point two one crore**. Monthly surplus **eleven point
> three lakh**. Working capital limit **one point four four crore**, computed on
> the **Nayak turnover method** — twenty per cent of turnover, five per cent
> borrower margin. Term loan capacity **four point one seven crore** from EMI
> headroom.
>
> Indicative. Never a sanction — the sanction is always yours.
>
> Below that, fourteen appraisal ratios — DSCR, FOIR, banking penetration, GSTR-1
> against 3B, ITC claimed against available — each marked Strong, Adequate or
> Weak."

### 5:15 – 5:50 · The opposite case

**[Switch account → Navdeep Auto Spares, Financial Score]**

> "Same screen, opposite story.
>
> **Three hundred and seventy-six.** At Risk. Cash Flow Health **zero out of two
> hundred**. Thirteen bounce and return events. EMI is seventy-five per cent of
> inflow. Term loan capacity — **zero**. There is no repayment capacity here and
> the system says so plainly.
>
> And this banner" — **point at the anomaly flag** — "is a data-integrity signal.
> GST turnover, declared income and actual bank credits disagree by twenty-three
> per cent. Deliberately, that is **not** a score deduction. It's a flag for your
> officer to review. We don't silently punish a business for a mismatch we can't
> explain."

### 5:50 – 6:00 · Handover

> "That's the borrower's view. Thakre will show you what your credit officer sees."

**→ HANDOVER.** Driver switches to Tab 2 (bank portal, Customers list).

---

# 3 · Thakre — The banker's view
### 6:00 – 10:30 · Live app, bank portal

### 6:00 – 6:40 · Portfolio, then one customer

**[Tab 2 · Bank Dashboard first, then Customers]**

> "This is the bank side. Portfolio view first — how many MSMEs onboarded, how
> many scored, how many are lendable today, how many are flagged. Band
> distribution, and the onboarding trend.
>
> Then the customer list — searchable, filterable by score band, by state, by
> whether analysis has run."

**Point down the score column.**

> "Eight-thirteen. Eight-oh-six. Seven-fifteen. Six-twenty-seven. Three-seventy-six.
> One screen, and an officer already knows which files to open first."

### 6:40 – 7:40 · Customer 360

**[Open Shreeji → the tabs]**

> "Open any one and you get Customer 360. Everything the bank needs, one screen,
> six tabs.
>
> **Financial Score** — the card you just saw.
> **Udyam Identity** — vintage, NIC activities, plant locations.
> **GST Analysis** — filing pattern, turnover trend, the deep-dive.
> **Bank** — the Account Aggregator statement analysis: inflow trend, bounces, EMI
> load, counterparty diversity.
> **ITR** — filing timeliness, tax paid, audit status, and the GST-to-income
> reconciliation.
> **Corporate** — company, directors, charges.
>
> The officer isn't opening six systems and one PDF. It's one screen."

### 7:40 – 8:40 · The charges story — your strongest minute

**[Switch to UCN Fibrenet (715) → Corporate (MCA) tab]**

> "This one matters most to a lender, so let me stay on it.
>
> UCN Fibrenet — a private limited company, scored **seven-fifteen** on **real
> six-month GST filings**, not generated data.
>
> Company profile from MCA. Two directors, both **DIN-verified**, each with their
> own PAN — and you can drill into either one."

**Click a director. Show the profile. Close it.**

> "And this is the panel I'd point a credit officer at: **five registered
> charges. Four open, totalling eleven point five two crore. One satisfied.**
>
> That is eleven and a half crore of assets already pledged to other lenders —
> read off the public register, without asking the borrower, and without waiting
> for a declaration they may or may not make."

**Now switch to Shreeji's Corporate tab.**

> "And here's a partnership firm. No CIN, no directors, no charges — so the
> platform shows nothing rather than inventing something. The screen follows the
> constitution of the business."

### 8:40 – 9:30 · Monitoring — the new part

**[Back to a customer → click Score History]**

> "Here's what's new since the first round, and it's the part we think matters
> most to a bank.
>
> Scoring at application is useful. But your exposure doesn't end at
> disbursement.
>
> Every time we analyse a business, we keep the result — append-only, never
> overwritten. So you get the **series**, not just today's number. Overall score on
> one axis, cash flow health and compliance on the other, each point coloured by
> band.
>
> A bureau gives you a one-time assessment. This gives you a **trend** — and a
> trend is what an early-warning system is built on. If an account drops a band, if
> GST filings lapse, if a new charge appears — those are all comparisons between
> two points on this chart."

### Optional · 45 s · The hand-off — LOS / ULI / ONDC

**[Same customer → Push to LOS / ULI / ONDC → pick LOS → Preview payload]**

> "One last thing, because it's the question every bank asks: what do we actually
> hand you?
>
> The officer raises this as a loan case — into your **LOS**, into **ULI**, or onto
> **ONDC**. Three different standards, three different payload shapes, same score
> underneath.
>
> And before anything is sent, they see the payload. This is the LOS appraisal
> case: applicant, eligibility, all six dimensions, the ratio table, and where every
> number came from. Nothing is sent until they confirm.
>
> On ULI it looks different on purpose — the score goes across as a **derived data
> packet** next to the source packets and the consent artefacts, with no decision
> block, because under ULI the lender decides and we supply the evidence.
>
> Today no endpoint is configured, so this is **recorded, not sent** — and the badge
> says so. Point it at your LOS URL and the same code path posts it. That is a
> setting, not a project."

> ⚠️ Optional — it is not in the 12-minute budget. Run it only if the room is
> technical and you are ahead of the clock; the 45 s comes out of the Ratnadeep
> judgement case below. Cut this before you cut the charges panel or score history.

### 9:30 – 10:30 · Explainability & the judgement case

**[Open Ratnadeep Textile Traders (627)]**

> "Last one — and it's the most realistic file in the set.
>
> **Six-twenty-seven.** Fair. Not an obvious yes, not an obvious no. The kind of
> file your officer actually spends time on.
>
> The score doesn't say no. It says why it isn't higher: **two GST periods
> missed**. Income tax return filed **late**, under 139(4). GST-to-income variance
> **six point four per cent** — outside tolerance. Two cheque returns.
>
> And look at the product recommendation. For this file the only product offered
> is **CGTMSE-backed working capital**. Not a clean limit — a guarantee-backed one.
>
> That's the whole argument in one screen. The score doesn't just rank the
> borrower. It routes them to a product that makes the exposure acceptable."

**→ HANDOVER.**

> "Abhinav will close on where we are."

---

# 4 · Abhinav — Where we are & the ask
### 10:30 – 12:00 · Slides

### 10:30 – 11:00 · Be precise about readiness

> "Quickly, on what's real.
>
> **Working today:** end-to-end onboarding, the scoring engine, the Health Card,
> Udyam, GST, ITR, MCA and DIN screens, the AA consent journey, the printable
> report, the bank portal, score history, the loan-case hand-off to LOS, ULI and
> ONDC, and seven languages.
>
> **Running on:** cached and synthetic responses in the documented API shapes. We
> submitted our Data Field Requirements covering all seventeen APIs.
>
> **Not yet done:** live sandbox access, EPFO, and the *inbound* score API — the
> one a lender calls us on. The outbound direction is built: we can raise the case
> into LOS, ULI or ONDC today, and because no endpoint is configured yet, the push
> is recorded rather than sent — and the screen says so."

> ⚠️ Say this plainly. Bank teams respect precision and will find the gap anyway.
> Overselling readiness is the fastest way to lose a technical room.

> ⚠️ If asked "so is the LOS integration real?" — the honest answer is: the payload
> is real, the transport is one config line. Say exactly that. Do not claim a live
> LOS connection.

### 11:00 – 11:35 · What's next

> "Four things are next, in this order.
>
> **One — the early warning system.** Score history is in place; the rules on top
> of it are the next build. Band drop, GST lapse, a new charge, a bounce spike.
>
> **Two — a secured, documented API.** We can already push a case *to* you; what's
> next is you pulling *from* us — JWT plus an OpenAPI spec, so your LOS team has
> something concrete to code against. The same endpoint answers the LOS question,
> the LMS monitoring question, and the ULI/OCEN question.
>
> **Three — a credit policy layer.** Approve, refer, decline — with cut-offs your
> policy team edits, hard knock-outs, and officer overrides captured with a reason.
>
> **Four — a DPDP consent ledger** with customer-side revoke and auto-purge."

### 11:35 – 12:00 · The close and the ask

> "To put it simply — every finalist here will show you a score. A score is a
> feature.
>
> **We score them, we keep watching them, and we can plug into your LOS.**
> Origination, monitoring and integration is a system a bank can buy.
>
> What we'd most like from you is direction on **where this sits in your process**
> — is it a pre-sanction triage tool, or an input inside credit appraisal? Because
> that changes what we build next.
>
> Thank you. We're happy to take questions."

**Driver: return to Customer 360 and leave it on screen for Q&A.**

---

# Q&A — who answers what

Agree this now. Nothing reads worse than three people starting to answer at once.

| Topic | Answers | One-line position |
|---|---|---|
| Credit policy, weights, cut-offs, bands | **Abhinav** | Weights are 25/20/15/15/15/10 and configurable — ask them what *their* policy weighs |
| Lending maths — Nayak, DSCR, FOIR, term loan | **Abhinav** | Nayak: 20% of turnover, 5% margin, band-adjusted. Always indicative |
| Onboarding, consent, AA flow, languages | **Mayur** | Consent is explicit, artefact-backed and revocable |
| The score engine, ML, explainability | **Mayur** | 60% rules / 40% LightGBM. PFI reasons, template-based, never free text |
| Bank portal, MCA, charges, score history | **Thakre** | Charges come from the public register — no borrower declaration needed |
| Loan-case push — LOS / ULI / ONDC | **Thakre** | The payload is real and shown before sending; the endpoint is one config line. Never claim a live connection |
| Architecture, data storage, security | **Thakre** | One deployable, SQL + Blob per MSME, CSP headers, OTP login |
| Anything about timelines or commercials | **Abhinav only** | "Let us come back to you with a number we've checked as a team" |

### Likely questions — short answers

**"Is this a credit decision?"**
> No. It's an indicative assessment and a product recommendation. The sanction is
> always the bank's. We deliberately never use the word approved.

**"How does this plug into our LOS?"**
> From Customer 360 the officer raises the case, previews the exact JSON, and
> confirms. Today no endpoint is set, so it is recorded as simulated with that
> payload. Set `LoanCase:Endpoints:LOS` to your URL and the same action posts it —
> no code change. ULI and ONDC are the same mechanism with the envelope each
> standard expects.

**"What if the MSME gives no AA consent?"**
> We redistribute that dimension's weight across the sources we do have, and we
> disclose on the card that the score ran on reduced inputs. We never zero a
> dimension and we never fabricate one.

**"How do we know the ML model isn't a black box?"**
> Sixty per cent of the score is a transparent rules formula you can read. The ML
> forty per cent is a calibration on top. And every dimension ships a plain-language
> reason drawn from a fixed template dictionary — not generated text. That's what
> makes it auditable under the digital lending guidelines.

**"Where does the data live?"**
> Raw responses per MSME in blob storage, one folder per Udyam number, plus a SQL
> index of scores and history. Deployment model and residency is your call — it's a
> single deployable, so on-prem or your private cloud both work.

**"How is this different from a bureau score?"**
> A bureau needs a credit history. Our entire target segment doesn't have one. And
> a bureau gives you one number at one moment — we keep the series.

**"Can you integrate with our LOS?"**
> The scoring API exists. What it needs before you'd accept it is JWT and a
> published OpenAPI spec, and that's our next build. We'd rather tell you it's a
> week away than tell you it's done.

### If you don't know

> **"I don't know — let me come back to you on that."**

Say it. Write it down. It costs you nothing and guessing costs you the room.

---

# If something breaks

| Problem | Do this |
|---|---|
| A fetch spins too long | Don't wait. "This normally takes a few seconds — let me move to an account that's already loaded." Switch tabs |
| Internet drops | Phone hotspot. Keep talking to the slides while the driver reconnects |
| A page errors | Don't debug on screen. Go back, take another route to the same point. Note it and move on |
| You're running over | Skip §3 Ratnadeep (9:30–10:30) entirely. Go straight to Abhinav's close |
| A judge interrupts with a question | **Answer it.** Their questions are more valuable than finishing your section. The driver holds the screen still |

---

# The 8-minute version

If they cut you short, drop these and nothing stops making sense:

| Cut | Saves |
|---|---|
| §2 live onboarding (2:00–2:45) — start on the loaded dashboard instead | 45 s |
| §2 Navdeep contrast (5:15–5:50) | 35 s |
| §3 portfolio dashboard (6:00–6:20) — go straight to the customer list | 20 s |
| §3 Ratnadeep judgement case (9:30–10:30) | 60 s |
| §3 the loan-case hand-off — it is optional to begin with | 45 s |
| §4 "what's next" (11:00–11:35) — compress to one sentence | 35 s |

**Never cut:** the charges panel (7:40) and score history (8:40). Those two are
what separate this from a dashboard, and they're the two a banker remembers.
