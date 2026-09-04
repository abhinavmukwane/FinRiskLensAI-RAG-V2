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
- 12/12 GST returns filed, turnover trend **+0.79**, credit notes 0.6%
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
- **2 GST periods missed** (filing regularity 0.83), flat turnover
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
- **Half the GST returns missing** (regularity 0.50), turnover trend **−0.16**
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

Blob container `msme-data`: **92 / 78 / 43 files** in the three UAN folders —
`udyam.json`, `gst_taxpayer.json`, 12 months × 7 GST files, `aa.json`, `itr.json`,
`mca.json`, 2 × `DIN_*.json`, and a precomputed `result.json`.

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
5. **Close on explainability.** Every number traces to a source file; the score
   breakdown names the reason, not just the value.
