# IDBI Live Demo — three seeded MSME accounts

Three synthetic customers covering the score range, published to **UAT**
(`103.21.58.192` / `FinRiskLensAI_UAT`) and the `msme-data` blob container.

Generated 2026-09-04. Data is deterministic — regenerating produces byte-identical
files and the same scores.

---

## Logging in

Go to `/Auth/CustLogin`, enter the email, press **Generate OTP**. The app shows
the OTP in a modal on screen — *"This OTP is shown here for development purposes
only and won't appear once email delivery is live."* — so you read it off the
screen and type it into the six boxes. **No mailbox is involved.**

Verified end to end for all three accounts: OTP issued, validated,
`Login successful.` → `/Dashboard/CustDashboard`.

The `@finrisklens.demo` addresses are only identifiers. The SMTP server accepts
the message for relay and it bounces later out of band, which does not affect the
login. If you ever switch the app to real delivery, point these three at a mailbox
you control first (plus-addressing works: `abhinavmukwane+shreeji@gmail.com`).

Bank portal: `/Auth/BankLogin`, user `bankadmin` (password unchanged).

## The three accounts

| # | Enterprise | UAN | Score | Band | Login email |
|---|---|---|---|---|---|
| A | SHREEJI PRECISION COMPONENTS | `UDYAM-MH-20-0091447` | **813** | Excellent | `shreeji.precision@finrisklens.demo` |
| B | RATNADEEP TEXTILE TRADERS | `UDYAM-GJ-01-0044219` | **627** | Fair | `ratnadeep.textiles@finrisklens.demo` |
| C | NAVDEEP AUTO SPARES | `UDYAM-UP-28-0007733` | **376** | AtRisk | `navdeep.autospares@finrisklens.demo` |
| D | SANKALP AUTOTECH PRIVATE LIMITED | `UDYAM-MH-25-0042817` | **806** | Excellent | `sankalp.autotech@finrisklens.demo` |
| E | M/S UCN FIBRENET PRIVATE LIMITED | `UDYAM-MH-20-0067394` | **715** | Good | `mkhangar01@gmail.com` |

---

## A — SHREEJI PRECISION COMPONENTS · 813 · Excellent

Partnership firm, Pune. Precision metal components, 11 years old, 3 plants.
**The "approve it" case.**

| Dimension | Score |
|---|---|
| Revenue Vitality | 186 / 250 |
| Cash Flow Health | 169 / 200 |
| Transaction Trustworthiness | 143 / 150 |
| Compliance Quotient | 147 / 150 |
| Business Stability | 136 / 150 |
| Debt Serviceability | 78 / 100 |

- Annual turnover **₹7.21 Cr**, monthly surplus **₹11.3 L**
- Eligibility: **₹1.44 Cr** working capital + **₹4.17 Cr** term
- 12/12 GST returns filed, cashflow trend **+0.21**, credit notes 0.6%
- 84 days cash on hand, **zero bounces**, EMI only 3% of inflow
- ITR-5 filed on time, e-verified, processed, tax fully paid, 44AB audit complete
- GST↔ITR turnover variance **0.6%** — the two filings agree
- Products offered: Standard Working Capital Loan, Business Term Loan

**Talking point:** every source corroborates every other. This is what a clean
file looks like when all six dimensions are populated.

---

## B — RATNADEEP TEXTILE TRADERS · 627 · Fair

Partnership firm, Surat. Textile wholesale, 5.5 years old.
**The "this one needs judgement" case — the most useful in the room.**

| Dimension | Score |
|---|---|
| Revenue Vitality | 146 / 250 |
| Cash Flow Health | 92 / 200 |
| Transaction Trustworthiness | 117 / 150 |
| Compliance Quotient | 107 / 150 |
| Business Stability | 101 / 150 |
| Debt Serviceability | 72 / 100 |

- Annual turnover **₹1.44 Cr**, monthly surplus **₹1.05 L**
- Eligibility: **₹17.3 L** working capital + **₹23.2 L** term
- **2 GST periods missed** (Oct + Nov 2025; regularity 0.83), flat turnover (trend +0.03)
- 39 days cash, **2 bounces**, credit notes 4.5%
- ITR filed **late** under 139(4); GST↔ITR variance **6.4%** — outside tolerance
- Only product offered: **CGTMSE-backed Working Capital** (guarantee-backed,
  not clean lending)

**Talking point:** the score doesn't say no — it says *this is why it isn't higher*,
and steers the officer to a guarantee-backed product instead of a clean limit.

---

## C — NAVDEEP AUTO SPARES · 376 · AtRisk

Proprietorship, Kanpur. Auto spares retail, 13 months old.
**The "decline, and here's the evidence" case.**

| Dimension | Score |
|---|---|
| Revenue Vitality | 124 / 250 |
| Cash Flow Health | **0 / 200** |
| Transaction Trustworthiness | 81 / 150 |
| Compliance Quotient | 56 / 150 |
| Business Stability | 43 / 150 |
| Debt Serviceability | 35 / 100 |

- Annual turnover **₹46.9 L**, monthly surplus **−₹4.94 L** (burning cash)
- Eligibility: ₹3.3 L working capital, **₹0 term** — no repayment capacity
- **7 of 12 GST periods missing** (regularity 0.42), cashflow trend **−0.38**
- **Zero days cash on hand**, **13 bounce/return events**, EMI **75% of inflow**
- Cash-heavy: 42% of receipts arrive as cash deposits
- ITR-4 presumptive, filed late, **not e-verified**, **not processed**,
  only 55% of tax paid, **demand outstanding**
- GST↔ITR turnover variance **23.5%**
- **Cross-source anomaly FLAGGED** — GST turnover, ITR income and bank credits
  disagree materially
- Only product offered: MUDRA (Shishu/Kishor)

**Talking point:** the anomaly flag is deliberately *not* a score deduction — it's
a data-integrity signal for officer review. Worth calling out to the IDBI team.

---

## D — SANKALP AUTOTECH PRIVATE LIMITED · 806 · Excellent

**The complete file — every source populated, including MCA and DIN.
This is the one to open when you want to show everything at once.**

Private Limited Company, Nashik. Auto components (NIC 29), incorporated
**2014-06-18** — 12 years old, 2 plants at MIDC Satpur.

| | |
|---|---|
| UAN | `UDYAM-MH-25-0042817` |
| CIN | `U29304MH2014PTC257318` |
| GSTIN / PAN | `27AAJCS7412K1Z0` / `AAJCS7412K` |
| Login | `sankalp.autotech@finrisklens.demo` |
| Blob | **93 files** — 12 months x 7 GST, udyam, gst_taxpayer, aa, itr, mca, 2 x DIN, result |

| Dimension | Score |
|---|---|
| Revenue Vitality | 186.2 / 250 |
| Cash Flow Health | 159.1 / 200 |
| Transaction Trustworthiness | 142.9 / 150 |
| Compliance Quotient | 147.4 / 150 |
| Business Stability | 135.4 / 150 |
| Debt Serviceability | 78.4 / 100 |

- Annual turnover **Rs 8.51 Cr**, monthly surplus **Rs 13.4 L**
- Eligibility **Rs 6.62 Cr** — Rs 1.70 Cr working capital + Rs 4.92 Cr term
- Products: Standard Working Capital Loan, Business Term Loan
- **ITR-6** (company return), 139(1) on time, e-verified, processed, 44AB audit
  complete, tax fully paid, GST-ITR variance 0.6%
- **Corporate (MCA): score 90/100, LOW risk** — Active, RoC-Mumbai, authorised
  Rs 2.5 Cr > paid-up Rs 1.8 Cr, last AGM 2025-09-26, **3 charges (2 OPEN
  Rs 1.55 Cr + 1 SATISFIED)**, 2 DIN-verified directors with individual PANs
  and full drill-down profiles
- No anomaly flag; cashflow trend +0.21

**Talking point:** this is the "all six dimensions, all seven sources" file.
Open the MCA tab for the collateral view, then switch to Shreeji (a partnership)
where the same tab correctly reads *"no CIN, no DIN"*. Same platform, different
constitution, different registries.

**Provenance:** built 2026-09-07 by cloning Shreeji's financial data, scaling
every monetary value by 1.18, and giving it a full corporate identity + ITR-6 +
clean MCA/DIN. The score was **not** hand-set — the files were uploaded and
`POST /api/msme-data/{uan}/analyze?force=true` produced 806 from the real engine.

---

## E — M/S UCN FIBRENET PRIVATE LIMITED · 715 · Good

**A second corporate file — real GST data, a mid-band score.**

Private Limited Company, Nagpur. Broadband/telecom (NIC 61), incorporated
2016-12-15. Scored on **real 6-month GST data**, not generated returns.

| | |
|---|---|
| UAN | `UDYAM-MH-20-0067394` |
| CIN | `U61909MH2016PTC089342` |
| GSTIN / PAN | `27AACCU0242R1Z1` / `AACCU0242R` |
| Login | `mkhangar01@gmail.com` |

- **Corporate (MCA) tab has full data** — company profile, 2 directors with DIN
  drill-down, and **5 registered charges: 4 OPEN (₹11.52 Cr) + 1 SATISFIED**
- Authorised capital ₹6 Cr > paid-up ₹4.6 Cr; last AGM 2025-09-29; MCA
  incorporation date matches Udyam
- Directors Kavita Anil Deshmukh (DIN 64259204) and Rajesh Madhukar Joshi
  (DIN 15910236), each with an individual PAN and this CIN in
  `companies_associated`

**Talking point:** the charges panel is the lender's collateral view — ₹11.52 Cr
already charged to other lenders, one facility closed. That is exactly the
"is this asset already pledged?" question a credit officer asks, answered from
the public register without asking the borrower.

Cleaned 2026-09-07 alongside A–C: the seeded MCA record had directors named
"M/S Kavita" / "Rajesh M/S", both carrying the *company's* PAN, an invalid
`m/s.…@example.com` email, authorised capital below paid-up, and a last AGM of
2019 on an Active company. Score is untouched (715) — MCA has no feature
extractor, so it never fed the score.

> The other five company accounts in the portal (TRUST FINTECH, PREMIENT ENGITECH,
> Nair Textiles, Reddy Industries, Iyer Enterprises) still carry the same
> generator defects. Demo MCA on **D** or **E** only.

---

## Score history

`t_MsmeScoreHistory` is append-only: one row per analysis run, never updated.
`t_MsmeScoreSummary` still holds the current value for the portfolio list; this
table holds the series behind the **Score History** button on the Financial
Health Card.

Eight monthly observations were seeded per account on 2026-09-07 so the trend
chart has something to draw — the table was created empty. Every seeded row
carries `Source = 'seed'`; a real run appends `Source = 'analysis'`, so
fabricated points stay distinguishable forever. The **last** seeded point is the
score the engine actually computed, so the chart's end agrees with the card.

| Account | Series | Story |
|---|---|---|
| A · Shreeji | 762 → **813** | Good → Excellent, steady climb |
| D · Sankalp | 741 → **806** | Good → Excellent, steady climb |
| B · Ratnadeep | 692 → **627** | **Good → Fair** — the band drop an EWS rule fires on |
| C · Navdeep | 549 → **376** | **Fair → AtRisk** — the account you wish you had watched |

Re-seed or reset with `Doc/sql/demo_score_history_seed.sql`; it deletes only
`Source = 'seed'` rows, so real analysis history survives.

**Talking point:** open Ratnadeep's Score History. The card says 627 today; the
chart says it was 692 in February and has fallen every month since. Origination
scoring would have approved it in February. That gap is the argument for
continuous monitoring.

---

## Loan-case pushes

`t_LoanCasePush` records every attempt to raise a scored MSME as a loan case in
**LOS**, **ULI** or **ONDC** from bank Customer 360 (**Push to LOS / ULI / ONDC**
in the crumb bar). Append-only — a re-push after a re-score is a real event, not a
correction.

Current state, seeded by an actual push on 2026-09-15 (not hand-inserted):

| ID | Account | Channel | Status | Case reference | Score at push |
|---|---|---|---|---|---|
| 2 | D · Sankalp | LOS | Simulated | `IBKL-LOS-20260915-934261` | 806 |
| 3 | D · Sankalp | ULI | Simulated | `IBKL-ULI-20260915-E44E4C` | 806 |
| 4 | D · Sankalp | ONDC | Simulated | `IBKL-ONDC-20260915-2BA5A9` | 806 |

All three are **Simulated** because `LoanCase:Endpoints:*` is blank — the payload
was built and recorded exactly as it would have been sent, and the chip says `sim`.
That is deliberate: a banking jury will ask whether the integration is real, and
"recorded, not sent, and the screen says so" is a better answer than a faked 200.

**Before a live demo, decide which you want:**

- *Show the push happening* — clear Sankalp's rows first, so the officer raises the
  case on stage:
  ```sql
  DELETE FROM [FinRiskLensAI].[t_LoanCasePush] WHERE Uan = 'UDYAM-MH-25-0042817';
  ```
- *Show the "already raised" state* — leave them; the three chips render on load and
  the modal warns before a re-push.

A chip turns **amber (`re-score`)** when the current score has moved away from
`ScoreAtPush` — re-analyse Sankalp after pushing and you get that state for free.

Any other demo account works too: push from B · Ratnadeep (627) to show a Fair-band
case going downstream with the band frozen on the receipt.

---

## Registry identity

Corrected 2026-09-07 after an audit of the seeded files. Every GSTIN now carries a
**valid check digit** (verified with the same algorithm as
`DummyDataService.BuildGstin`), and each PAN's 4th character matches the
constitution — `F` = firm, `P` = individual/proprietor.

| | Constitution | PAN | GSTIN | ITR form |
|---|---|---|---|---|
| A · Shreeji | Partnership | `AABFS4417K` | `27AABFS4417K1Z3` | ITR-5 |
| B · Ratnadeep | Partnership | `AAGFR2210M` | `24AAGFR2210M1ZE` | ITR-5 |
| C · Navdeep | Proprietorship | `AFQPN8123L` | `09AFQPN8123L1ZR` | ITR-4 (SUGAM) |

The 36 counterparty GSTINs in the GSTR-1/2A/2B files were placeholders
(`27CUST00PAN01Z5`, some only 14 characters). They are now structurally valid and
map 1:1 to the old ones, so customer/vendor concentration is unchanged.

**No `mca.json` or `DIN_*.json`.** None of these three is a company, so none can
hold a CIN or a DIN — the seeded files previously gave all three a *public limited
company* record with incorporation dates up to 17 years off the Udyam date. The
MCA tab correctly shows "not applicable" for all three.

> **Demo note:** that is a talking point, not a gap — the platform picks the
> registries that apply to the constitution. Use **account D** below to show the
> MCA/DIN screens live.

Addresses are now state-appropriate (MIDC → Pune only; GIDC Pandesara for Surat,
UPSIDA Panki for Kanpur), `major_activity` matches the NIC division (Trading for
B and C, whose NIC codes are 46 and 45), and ITR filing dates sit in the past
while preserving each return's on-time / late status.

**Scores are unaffected** — `result.json` is byte-identical for all three
(813 / 627 / 376), because only identity fields changed. `nic_code` was left
untouched, since it drives `SectorRiskWeight`.

### Still open (code, not data)

Three items an alert judge could still probe — they need engine changes, not
new files:

1. `GSTR-1 vs GSTR-3B Consistency` returns **0.18 for all three** — a constant,
   so it renders red even on the 813 file.
2. `ITC Claimed vs 2B Available` is **0.0** despite `Has2bData: true`.
3. `GstTurnoverVariancePct` compares two fields that both live *inside*
   `itr.json`, so it never checks ITR against the actual GST returns.



## What was written to UAT

| Table | Rows |
|---|---|
| `t_MsmeEnquiry` | ids 13, 14, 15 |
| `t_UserRegistration` | ids 11, 12, 13 |
| `t_MsmeScoreSummary` | one per UAN |
| `t_UserOtp` | ids 11, 12, 13 |

`ValidateCustomerEmail` looks up an existing `t_UserOtp` row by email and returns
`"OTP record not found."` if there is none — real onboarding creates it during
registration. Seeding a customer without that row makes login impossible, so any
future hand-seeded account needs all four tables, not three.

Blob container `msme-data`: **89 / 76 / 41 files** in the three UAN folders —
`udyam.json`, `gst_taxpayer.json`, 12 / 10 / 5 months × 7 GST files, `aa.json`,
`itr.json`, and a precomputed `result.json` (plus `_manifest.json` for B and C).
No `mca.json` / `DIN_*.json` — see “Registry identity” below.

`result.json` is precomputed so the Financial Health Card and the bank portal
render immediately. Pressing **Re-analyze Score** recomputes from the same blobs
and lands on the same number — verified by reading the files back out of Azure
through `AzureBlobDataStore` and re-running `RiskScoringService`.

### Rollback

`Doc/sql/demo_accounts_rollback.sql` removes exactly these rows (all four tables). Blob folders
must be deleted separately (Azure Storage Explorer → `msme-data` → delete the
three `UDYAM-…` folders).

---

## Suggested 5-minute run of show

1. **Bank portal → Customers.** All three in one list, scores 813 / 627 / 376,
   bands colour-coded. One screen, three decisions.
2. **Open Shreeji (813).** Financial Score tab — gauge, six dimensions, then the
   lending assessment: ₹1.44 Cr WC. Show that every tab has data.
3. **Open Navdeep (376).** Same screen, opposite story. Cash Flow Health 0/200,
   the anomaly banner, ₹0 term capacity.
4. **Open Ratnadeep (627).** The judgement case — point at the two missing GST
   periods and the 6.4% GST↔ITR variance, then at CGTMSE being the only product.
5. **Open Sankalp Autotech (806) → Corporate (MCA).** The complete file — MCA
   score 90/100, 2 open charges worth ₹1.55 Cr plus one satisfied, two
   DIN-verified directors with full drill-down. Then flip back to Shreeji's MCA
   tab: *"partnership firm — no CIN, no DIN."* Same platform, different
   constitution, different registries.
6. **Close on explainability.** Every number traces to a source file; the score
   breakdown names the reason, not just the value.
