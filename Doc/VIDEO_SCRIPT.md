# FinRiskLens — 3-Minute Demo Video

**Google AI Builder Cup 2026 · BFSI track · Team AS FinTech Developer**

The customer flow ([`12_CUSTOMER_RISK_LENS.md`](12_CUSTOMER_RISK_LENS.md)): the customer shares
data → the bank's Risk Lens for that customer → a Clear customer's loan case with the AI credit
decision → LOS / ULI / ONDC. Recorded on the live site (<https://finrisklensai.cloud.run>) with the
demo customers (`UDYAM-MH-99-0000301` … `0308`). Every customer shown is synthetic. About 430 words
of voiceover at a calm pace; each block lists what is on screen and what is said.

---

### 0:00 – 0:15 · Opening

**Screen:** home page.

> An MSME applies for a loan. Its bank statements, GST returns and ITR all look right. FinRiskLens
> checks every one of them the way a forensic investigator would — and shows the evidence for
> everything it finds.

### 0:15 – 0:40 · The customer shares data

**Screen:** customer dashboard — Account Aggregator consent, GST, ITR and MCA fetched, then
**Upload documents** with a bank statement PDF; the upload shows *Reading*.

> The business registers with its Udyam number, shares its bank data through Account Aggregator
> consent, fetches its GST, ITR and company records, and uploads its documents. Every new source
> reassesses the customer. Gemini on Vertex AI reads each page in Mumbai, so the documents never
> leave India.

### 0:40 – 0:55 · One page per customer

**Screen:** bank portal → **Customers** — the risk column: Clear, High risk, Awaiting data.

> On the bank side, every customer has a risk status, and one page with everything they shared.

### 0:55 – 1:25 · The bank's own record

**Screen:** Edited statement (`…0302`) → Risk Lens. The verdict *High risk*; the finding
*Statement disagrees with the bank's own record*; click an evidence row — the PDF opens at its page.

> This statement passes every arithmetic check. But the bank's own record, fetched through Account
> Aggregator, does not agree: receipts inflated, loan EMIs removed. Every difference is in rupees,
> and every one links to its row.

### 1:25 – 1:50 · Linked customers

**Screen:** Circular traders (`…0306`) → Risk Lens → **Network** section.

> Across customers, FinRiskLens looks for what unrelated businesses never share. These three have
> the same director, and their GST invoices go round in a loop — sales that only exist on paper.
> Each one's page names the others.

### 1:50 – 2:10 · Grounded AI

**Screen:** any customer's Risk Lens → **Run investigation**; an AI finding with row and policy
evidence; the rejected-claims panel.

> A Gemini Pro investigator then reads the customer with tools bound to this one customer. Every AI
> claim must point to the customer's own rows, and every rupee must trace to them — anything that
> does not is set aside, and shown.

### 2:10 – 2:45 · Clear → the loan case

**Screen:** Clear customer (`…0301`) → **Loan case**: amount, tenure, rate → **Generate credit
decision** → the capacity figures, reasons with policy citations → **Accept** → **Push to LOS** → receipt.

> A customer with nothing open goes straight to a loan case. The credit capacity is computed in code
> under the lender's policy; Gemini Pro decides within it and cites the clauses. The officer accepts,
> and the loan case — with its risk assessment and decision — goes to the bank's LOS, to ULI, or to ONDC.

### 2:45 – 3:00 · Close

**Screen:** home page.

> FinRiskLens — fraud detection and risk analysis for every MSME customer, built on Gemini, Cloud
> Run, Cloud SQL and Cloud Storage. Every finding backed by evidence.

---

**Before recording:** sign in to the bank portal once so the session survives, and open the four
customers in tabs in this order — `…0302`, `…0306`, `…0301`, and one for the investigation — so
nothing waits on a page load. Run the investigation beforehand too; press it live only if time allows
(12–43 s). The demo customers' email addresses are fictional, so record the customer part with a
registration whose email you can read for the sign-in code.
