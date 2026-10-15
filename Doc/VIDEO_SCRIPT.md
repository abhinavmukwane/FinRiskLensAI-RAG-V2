# FinRiskLens — 3-Minute Demo Video

**Google AI Builder Cup 2026 · BFSI track · Team ASM FinTech Developer**

Recorded on the live site (<https://finrisklensai.cloud.run>), signed in to the bank
portal. Every case shown is synthetic and stamped as such. About 430 words of voiceover
at a calm pace; each block lists what is on screen and what is said.

---

### 0:00 – 0:15 · Opening

**Screen:** home page — "Evidence-based fraud detection".

> A loan officer receives a bank statement. Every balance adds up, every page total
> matches. FinRiskLens checks it the way a forensic investigator would — and shows the
> evidence for everything it finds.

### 0:15 – 0:35 · A new case

**Screen:** Cases → New case. Upload `demo-t04_narmada_statement_jun-sep.pdf`, Udyam
number `UDYAM-MH-99-0000204`. The case page refreshes while Gemini reads.

> The officer opens a case and uploads what the applicant submitted. Gemini on Vertex AI,
> running in Mumbai, reads every page in parallel and copies each number exactly as
> printed — so the documents never leave India.

### 0:35 – 1:05 · The bank's own record

**Screen:** DEMO-T04 findings. Expand *Statement disagrees with the bank's own record*;
click the row-28 evidence — the PDF opens at page 2.

> This statement passes every arithmetic check. But the applicant linked their account
> through Account Aggregator, so FinRiskLens compares it with the bank's own record.
> Ninety-one of ninety-three rows agree. Two receipts were tripled and four loan EMIs
> removed — the PDF shows three lakh sixty-five thousand more in credits than the bank
> recorded. Every difference links to its row.

### 1:05 – 1:25 · The document's own arithmetic

**Screen:** DEMO-T01, then DEMO-T02 — balance break, totals mismatch, file history.

> Simpler edits show in the document itself: a balance that no longer follows from the
> row before, page totals that disagree, a file last saved in Word or merged in iLovePDF.

### 1:25 – 1:55 · The investigator

**Screen:** DEMO-A01 → Run investigation. Findings *Undeclared EMIs* and *Loan stacking*,
with policy evidence. Then DEMO-A02 — gambling and crypto exposure.

> Then a Gemini Pro investigator reads the case with tools bound to this one application.
> It finds monthly EMIs to two lenders the application never declared, and cites the
> credit-policy clause. Every AI claim must point to rows of this case, and every rupee
> amount must trace to them — anything that does not is set aside, and shown.

### 1:55 – 2:20 · Fraud rings

**Screen:** Rings → Ring 1 (DEMO-R01 … R04); open DEMO-R01's ring finding.

> Across applications, the network lens looks for what unrelated applicants never share.
> These four statements claim two different banks and the slips three employers — yet
> one tool made every file, and one account pays every salary. One fabricator, four
> applications, found together.

### 2:20 – 2:40 · Measured

**Screen:** README → *Measured, not assumed*.

> Every number is measured against ground truth: up to one hundred percent of statement
> rows read exactly, every demo case raising exactly its expected findings, and zero
> false alarms on genuine applicants.

### 2:40 – 3:00 · Close

**Screen:** home page.

> FinRiskLens — evidence-based fraud detection and risk analysis, built on Gemini,
> Cloud Run, Cloud SQL and Cloud Storage. Every finding backed by evidence.

---

**Before recording:** sign in once so the session survives; open the cases in separate tabs
in this order — T04, T01, T02, A01, A02, Rings — so nothing waits on a page load. Run the
investigation on A01 beforehand too, and use **Run investigation** live only if time allows
(12–43 s).
