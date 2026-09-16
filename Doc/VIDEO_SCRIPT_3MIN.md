# FinRiskLens AI — 3-Minute Demo Video Script

**IDBI Innovate 2026 · Finals submission · Team ASM FinTech Developer**

The earlier [`FinRiskLensAI_Video_Script.md`](FinRiskLensAI_Video_Script.md) runs 7:30 and
goes deep on the ML engine. This is the **3-minute cut** the finals ask for: simple
language, short sentences, one idea per screen. Keep that file for reference — use
this one to record.

---

## How to use this

| | |
|---|---|
| **Target length** | 3:00 · **391 spoken words** — 2:48 at 140 wpm, exactly 3:00 at 130 wpm |
| **Language** | Plain English. Short sentences. Say numbers the way you'd say them out loud — "eight hundred and thirteen", not "813" |
| **Pace** | Read at a **relaxed 130–140 wpm**. The script is written 12 seconds short on purpose — that gap is your pauses at screen changes. Do not speed up to fill it |
| **Recording** | Record narration first, then screen-capture to match. Far easier than trying to talk and click at the same time |
| **Accounts** | Use **Shreeji Precision** (813) as the main story and **Navdeep Auto Spares** (376) for contrast. See [`DEMO_ACCOUNTS.md`](DEMO_ACCOUNTS.md) |

**Before recording:** log in on both sides in separate tabs so no screen shows a
login form or a loading spinner. Record at 1920×1080.

---

## 0:00 – 0:18 · The problem
**[SCREEN: title card, then the landing page]**

> India has **six point four crore** small businesses.
>
> Most of them cannot get a bank loan. Not because they are bad businesses — but
> because they have no credit history. No bureau file. So the bank has nothing to
> assess, and a good business gets rejected.

*(46 words · 18 s)*

---

## 0:18 – 0:36 · The idea
**[SCREEN: landing page — the six dimension cards]**

> But these businesses do create data every day. GST returns. Income tax filings.
> Bank statements.
>
> FinRiskLens AI collects that data — with the owner's permission — and turns it
> into one credit score the bank can actually use.

*(38 words · 18 s)*

---

## 0:36 – 1:02 · Signing up
**[SCREEN: Udyam number entry → Review & Proceed → OTP → dashboard]**

> Signing up needs **one number** — the Udyam registration number.
>
> We fetch the business profile automatically. The owner checks the details, gives
> consent, and confirms with an email OTP. That is the whole process. No documents.
> No branch visit.

*(39 words · 26 s)*

---

## 1:02 – 1:28 · Collecting the data
**[SCREEN: dashboard → GST fetch → AA consent (Finvu) → ITR → MCA · four cards turning green]**

> From the dashboard, the business connects **five sources**, one click each.
>
> GST returns show turnover and whether filings are on time. Income tax returns
> confirm declared income. Bank statements come through the RBI-regulated Account
> Aggregator — the owner approves on their bank's own screen. And for companies,
> MCA records — directors, and loans already registered against the business.

*(58 words · 26 s)*

---

## 1:28 – 2:02 · The score
**[SCREEN: Financial Health Card — gauge, then the six dimensions, then lending assessment]**

> Now the score.
>
> **Eight hundred and thirteen out of a thousand.** Band: Excellent.
>
> It is built from six parts — revenue, cash flow, transactions, compliance,
> stability, and ability to repay. Each one shows its points and the reason behind
> it. Filing gaps. Cheque bounces. Cash flow rising or falling.
>
> And it ends with a number a banker can act on: **one point four four crore** of
> working capital, and the loan product that fits.

*(74 words · 34 s)*

---

## 2:02 – 2:20 · The same screen, a weak file
**[SCREEN: switch to Navdeep Auto Spares — 376 · Cash Flow Health 0/200 · anomaly banner]**

> Here is a different business on the same screen.
>
> **Three hundred and seventy-six.** At risk. Cash flow health, zero. Thirteen
> bounced payments. And a warning flag — GST, tax and bank numbers do not match.
>
> It does not say no. It shows why.

*(43 words · 18 s)*

---

## 2:20 – 2:38 · The bank's view
**[SCREEN: Bank portal → customer list → Customer 360 → Score History chart]**

> On the bank side, every MSME is in one list with scores and bands. Open any one
> and everything is on a single screen — score, GST, bank, tax, company and
> directors.
>
> And every score is kept, so the bank sees the **trend**, not just today's number.

*(47 words · 18 s)*

---

## 2:38 – 2:52 · The hand-off
**[SCREEN: Push to LOS / ULI / ONDC → channel picker → payload preview → confirm → chips]**

> Then one action hands it over — into the bank's own lending system, into **ULI**,
> or onto **ONDC**. The officer sees the exact data before it goes.

*(27 words · 14 s)*

---

## 2:52 – 3:00 · Close
**[SCREEN: logo + tagline]**

> One score. Five sources. No paperwork.
>
> FinRiskLens AI — built by team ASM FinTech Developer, for IDBI Innovate 2026.

*(19 words · 8 s)*

---

## Screen order — shot list

| # | Screen | Approx. duration |
|---|---|---|
| 1 | Title card → landing page hero | 18 s |
| 2 | Landing page — six dimension cards | 18 s |
| 3 | Udyam entry → review → OTP → dashboard | 26 s |
| 4 | Dashboard — GST, AA/Finvu consent, ITR, MCA fetches | 26 s |
| 5 | Financial Health Card — Shreeji 813 (gauge → dimensions → lending) | 34 s |
| 6 | Financial Health Card — Navdeep 376 (cash flow 0, anomaly banner) | 18 s |
| 7 | Bank portal — list → Customer 360 → Score History | 18 s |
| 8 | Push to LOS / ULI / ONDC — channel → payload preview → confirm | 14 s |
| 9 | Logo / closing card | 8 s |

**Shot 8 note:** record it on **Sankalp Autotech (806)**, and clear its existing
pushes first if you want the chips to appear during the take — the SQL is in
[`DEMO_ACCOUNTS.md`](DEMO_ACCOUNTS.md) → *Loan-case pushes*. The badge will read
**Simulated**, which is correct and worth leaving visible; no endpoint is configured.

---

## If you need to trim

Cut in this order. Each cut is self-contained — nothing after it stops making sense.

1. **The MCA sentence** in §1:02 — saves 6 s, but you lose the charges angle
2. **The hand-off** (§2:38) — saves 14 s. Cut this before Navdeep: for a general
   audience the good-file / bad-file contrast lands faster than an integration story
3. **The whole Navdeep section** (§2:02) — saves 18 s. Last resort: the contrast
   between a good file and a bad one is the most convincing twenty seconds in the video

## Do not say

- **"Approved" or "sanctioned."** It is an *indicative* limit. The sanction is always the bank's.
- **"Real-time bank data."** We run on sandbox and synthetic responses in the documented API shapes, pending live access.
- **Any score that isn't on screen.** If the recording shows 813, say 813.
- **"Integrated with the bank's LOS."** The payload is real and the push is recorded;
  no endpoint is configured yet. "Hands it over" is the strongest honest phrasing,
  which is why the script uses it.
