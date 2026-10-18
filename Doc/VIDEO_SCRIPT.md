# FinRiskLens AI — Official 3 to 4 Minute Master Demo Video Script

**Competition:** The Google Cloud AI Builder Cup 2026  
**Track:** BFSI: Intelligent Risk, Fraud & Financial Experiences  
**Team:** AS FinTech Developer (Abhinav Mukwane & Sourabh Thakre)  
**Target Duration:** Exactly 3:45 to 3:55 minutes (~530 spoken words at a clear, steady pace)  
**Live Application:** [https://finrisklensai.cloud.run](https://finrisklensai.cloud.run)  

---

## Complete Screen-by-Screen Coverage Matrix

This script guarantees that **100% of the platform's screens** are demonstrated during the video:

| # | Screen / View | URL Path | Key Elements Demonstrated |
|---|---|---|---|
| **1** | **Home Page** | `/` | Hero value prop, Google Translate / Bhashini language selector, Theme switch |
| **2** | **Customer Onboarding / Sign-in** | `/Auth/CustLogin` | Udyam Number input & email verification flow |
| **3** | **Customer Dashboard** | `/Dashboard/CustDashboard` | Welcome banner, Sign-in IP security popup, Connect Data Sources, Smart Gating |
| **4** | **Registration Profile (Udyam)** | `/Udyam/UdyamDetails` | Enterprise activity, NIC classification, manufacturing plant locations |
| **5** | **Tax & Sales Compliance (GST)** | `/Dashboard/GSTAnalysis` | GSTR-2B & GSTR-3B filing punctuality, ITC trends, turnover trajectory |
| **6** | **Consent Banking Data (AA)** | `/AccountAggregator/AADetails` | Account Aggregator transaction flows, average balance, monthly inflows |
| **7** | **Income Tax Filing (ITR)** | `/Itr/ItrDetails` | Declared gross total income, tax paid, and filing compliance records |
| **8** | **Corporate Registry (MCA)** | `/Mca/MCADetails` | Company standing, DIN-holding directors, active incorporation status |
| **9** | **Financial Health Score Card** | `/Dashboard/FinancialHealthCard` | 0–1000 score gauge, 6 dimensions, RBI ratios (FOIR, DSCR), working capital limit |
| **10** | **Customer Risk Lens** | `/Dashboard/RiskLens` | Transparent borrower view of verified findings and assessment verdict |
| **11** | **Bank Admin Sign-in** | `/Auth/BankLogin` | Secure officer authentication (`bankadmin` / `Bank@123`) |
| **12** | **Bank Portfolio Dashboard** | `/BankAdmin/Dashboard` | Portfolio triage: Clear, Review, High Risk, and Awaiting Data breakdown |
| **13** | **Customer 360 Risk Lens & Forensics** | `/BankAdmin/Customer?uan=...` | Statement vs AA diff, ITR mismatch (T8), Network Graph (T7), Confirm/Dismiss |
| **14** | **Grounded AI Underwriting** | Customer 360 → Loan Case tab | FOIR credit capacity, Gemini 3.1 Pro decision with policy citations (§3.1) |
| **15** | **Open Protocols Payload Modal** | Loan Case → Push modal | Structured machine-readable JSON for Core Banking LOS, ULI (OCEN), and ONDC |

---

## Pre-Recording Setup: The 7-Tab Browser Strip

To record seamlessly within 3:50 minutes without waiting for any page loads, open Google Chrome in fullscreen (1080p / 1920×1080) with these tabs prepared in advance:

- **Tab 1:** Home Page `https://finrisklensai.cloud.run`
- **Tab 2:** Customer Dashboard logged in as Reddy Industries / `UDYAM-MH-99-0000301`
- **Tab 3:** Customer Financial Health Card (`/Dashboard/FinancialHealthCard`)
- **Tab 4:** Bank Portfolio Dashboard (`/BankAdmin/Dashboard`)
- **Tab 5:** High Risk Customer 360 for `UDYAM-MH-99-0000302` (Statement vs AA Mismatch)
- **Tab 6:** Network Syndicate Customer 360 for `UDYAM-MH-99-0000306` (Circular Trading Loop)
- **Tab 7:** Clear Customer 360 for `UDYAM-MH-99-0000301` (Loan Case & AI Decision ready)

---

## Timed Master Script & Screen Actions

### PART 1: THE MSME CUSTOMER JOURNEY (0:00 – 1:45)

#### Scene 1: Home Page & Global Accessibility (0:00 – 0:20) · *[Screen 1]*
**What to Show on Screen:**
- Start on the FinRiskLens AI Home Page (`Tab 1`).
- Move mouse over the language selector: click to show Hindi / regional language options via Google Translate & Bhashini.
- Click the Theme switcher toggle in top bar (switch between Classic Maroon/Gold and IDBI Dark Green/Orange).
- Scroll past the hero value statement: *"Every finding backed by verifiable evidence"*.

**Voiceover (Spoken):**
> "In India, micro and small businesses face a thirty-trillion-rupee credit gap, held back by manual underwriting and surging digital document forgery. 
> 
> Welcome to **FinRiskLens AI**, built for the Google Cloud AI Builder Cup in the BFSI track. Accessible in regional Indian languages and switchable bank themes, it unifies multi-source fraud forensics with an intelligent financial health experience."

---

#### Scene 2: Customer Onboarding & Data Connection Hub (0:20 – 0:45) · *[Screens 2 & 3]*
**What to Show on Screen:**
- Switch to the **Customer Dashboard** (`Tab 2`).
- Show the top welcome banner with enterprise name.
- Click the **Sign-in IP badge** in the header: show the popup displaying real-time IP risk audit, VPN/proxy check, and geolocation. Close popup.
- Scroll through the **"Connect Your Data Sources"** hub:
  - Highlight the Account Aggregator (AA) consent button.
  - Show GST, ITR, and MCA source retrieval cards.
  - Hover over the **Upload Documents** section (PDF/image statements).
- Highlight the **Smart Data Availability Gating**: point out how the Customer Risk Lens and Score Card cards appear once verified sources are linked.

**Voiceover (Spoken):**
> "An enterprise registers with its Udyam number. Our security audit automatically verifies connection integrity.
> 
> On the customer dashboard, business owners connect their official financial records in minutes: consent-based banking via Account Aggregator, GST returns, Income Tax, MCA registry filings, and document uploads. 
> 
> Our smart gating ensures borrowers only see active tools once their data is connected."

---

#### Scene 3: Connected Registry Source Deep-Dives (0:45 – 1:15) · *[Screens 4, 5, 6, 7 & 8]*
**What to Show on Screen:**
- Rapidly click through the customer sidebar navigation to show each source analysis page (2–3 seconds each):
  1. Click **Registration (Udyam)**: show business classification and manufacturing plants.
  2. Click **Tax & Sales (GST)**: show filing punctuality, ITC trends, and monthly turnover.
  3. Click **Banking Data (AA)**: show consent-verified transaction volume and cashflow buffers.
  4. Click **Income Tax (ITR)**: show gross total income and tax paid.
  5. Click **Corporate Registry (MCA)**: show CIN status and verified DIN directors.

**Voiceover (Spoken):**
> "Each source is analyzed transparently for the borrower:
> - Official Udyam registration activity and plant units.
> - GST sales compliance, monthly turnover, and tax credits.
> - Account Aggregator banking conduct and cash balances.
> - Income tax filings and Ministry of Corporate Affairs director records.
> 
> Every data point is standardized and stored securely in Google Cloud Storage in Mumbai."

---

#### Scene 4: Financial Health Score Card & Customer Risk Lens (1:15 – 1:45) · *[Screens 9 & 10]*
**What to Show on Screen:**
- Switch to `Tab 3` (or click **Financial Score** in sidebar):
  - Show the interactive **0–1000 Credit Health Score Gauge**.
  - Scroll through the **6 Dimensions**: Revenue Vitality, Cash Flow Health, Banking Discipline, Tax Compliance, Business Stability, and Operating Margins.
  - Highlight the statutory **RBI Lending Ratios**: FOIR, DSCR, Current Ratio, and recommended working capital limit.
- Click **Risk Lens** in sidebar (`Screen 10`):
  - Show the customer's transparent view of their verification findings and assessment verdict.

**Voiceover (Spoken):**
> "From these verified sources, FinRiskLens generates an empowering 6-Dimension Financial Health Card. 
> 
> Borrowers receive an intuitive zero-to-one-thousand credit score, clear statutory RBI lending ratios, and indicative loan limits. 
> 
> Business owners also view their Customer Risk Lens, giving them transparent feedback on their credit readiness before an underwriter ever reviews the file."

---

### PART 2: THE BANK ADMIN UNDERWRITING JOURNEY (1:45 – 3:25)

#### Scene 5: Bank Admin Login & Portfolio Triage (1:45 – 2:05) · *[Screens 11 & 12]*
**What to Show on Screen:**
- Switch to `Tab 4` (`/BankAdmin/Dashboard`).
- Briefly show the Bank Login interface, then land on the **Portfolio Dashboard**:
  - Show top summary metric tiles: Total Customers, Clear, Review, High Risk, Awaiting Data.
  - Scroll through the customer table: show how borrowers are categorized instantly.

**Voiceover (Spoken):**
> "Now let's switch to the lender's perspective. 
> 
> Logging into the Bank Portal, the credit committee sees the entire portfolio triaged automatically. Applicants are instantly categorized as Clear, Review, High Risk, or Awaiting Data. 
> 
> Lenders eliminate weeks of manual document checking, prioritizing genuine borrowers while immediately stopping fraud."

---

#### Scene 6: Deep-Dive Fraud Forensics & Network Lens (2:05 – 2:55) · *[Screen 13]*
**What to Show on Screen:**
- Switch to `Tab 5` (Customer 360 for `UDYAM-MH-99-0000302`):
  - Show the **Risk Lens** banner marked *High Risk*.
  - Click on the finding: **"Statement disagrees with the bank's own record"** (`STATEMENT_VS_AA_MISMATCH`):
    - Click the evidence link: show the PDF viewer opening at the exact row where credits were inflated and loan EMIs erased compared to Account Aggregator records.
  - Point to the **ITR Mismatch finding** (T8): show the 5-field comparison where declared income exceeded the filed return.
- Switch to `Tab 6` (`UDYAM-MH-99-0000306`):
  - Click the **Network** tab: show the detected **Circular Trading Ring** (three businesses invoicing in loops sharing a common DIN director).
- Show **Human-in-the-Loop Governance**:
  - Click the **Dismiss** button on a finding: show that a written justification citing credit policy is mandatory before any finding can be dismissed.

**Voiceover (Spoken):**
> "Here is fraud detection in action.
> 
> On this applicant, the uploaded PDF bank statement looks completely authentic and passes arithmetic checks. But when FinRiskLens cross-references it against the bank's raw Account Aggregator data, it detects that deposits were inflated and debt EMIs deleted! Every discrepancy is quantified in rupees and links to the exact row.
> 
> Our ITR check catches altered tax return figures, while our cross-source check flags turnover inflation across GST and banking.
> 
> Our Network Lens uncovers organized crime: three separate applicants circulating invoices in a loop with a shared director.
> 
> Credit officers retain full control: dismissing any finding strictly requires a recorded policy justification."

---

#### Scene 7: Grounded AI Underwriting & One-Click Protocol Push (2:55 – 3:35) · *[Screens 14 & 15]*
**What to Show on Screen:**
- Switch to `Tab 7` (Customer 360 for clean applicant `UDYAM-MH-99-0000301`):
  - Show the verdict: **Clear — No fraud findings detected**.
  - Open the **Loan Case** panel:
    - Point to the code-computed **Credit Capacity Calculator**: monthly assessed income, FOIR tier limit (55%), and eligible loan ceiling.
  - Click **"Generate Credit Decision"**:
    - Watch **Gemini 3.1 Pro** draft the structured appraisal note with specific credit policy citations (§3.1.2, §3.1.3).
    - Point to the **Factual Grounding Verifier** confirming zero hallucinations.
  - Click **"Accept Decision"**.
  - Click **"Push to LOS / ULI / ONDC"**:
    - Show the modal displaying the structured, machine-readable JSON payload ready for Core Banking LOS, the Unified Lending Interface (ULI / OCEN), and ONDC!

**Voiceover (Spoken):**
> "What happens when an applicant is genuine?
> 
> For a Clear customer, FinRiskLens automates underwriting. 
> 
> First, our deterministic engine calculates the borrower's debt capacity under policy FOIR limits. 
> 
> Then, **Gemini 3.1 Pro** acts as an autonomous credit officer, drafting a fully grounded credit decision. Every rupee figure is verified against raw data, and every reason cites the lender's credit policy.
> 
> With one click, the officer accepts the loan and dispatches the structured package directly to Core Banking, to the **Unified Lending Interface (ULI)**, or to **ONDC Financial Services**!"

---

### PART 3: ARCHITECTURE & CONCLUSION (3:35 – 3:55)

#### Scene 8: Google Cloud Architecture & Closing (3:35 – 3:55) · *[Architecture & Live URL]*
**What to Show on Screen:**
- Switch back to `Tab 1` or open the architecture slide / GitHub repository.
- Show the Google Cloud badge: *Vertex AI · Cloud SQL pgvector · Cloud Run in Mumbai (asia-south1)*.
- End on the live URL `https://finrisklensai.cloud.run` and Team AS FinTech Developer.

**Voiceover (Spoken):**
> "FinRiskLens AI is powered by Google Cloud:
> - **Gemini 3.5 Flash** extracts multi-page statements in parallel with ninety-nine point eight percent accuracy.
> - **Gemini 3.1 Pro** powers our grounded investigator and credit decisioning agent.
> - **Cloud SQL with pgvector** runs our hybrid policy search.
> - And our entire architecture runs on **Cloud Run** in Mumbai, ensuring complete data residency and regulatory compliance in India.
> 
> FinRiskLens AI makes MSME lending faster, transparent, and completely evidence-backed. 
> 
> Thank you!"

---

## Recording Timing & Pacing Checklist

| Segment | Timing | Spoken Words | Screens Covered |
|---|:---:|:---:|---|
| **Part 1: MSME Customer Experience** | 0:00 – 1:45 (105s) | ~250 words | **Screens 1–10** (Home, Onboarding, Dashboard, Udyam, GST, AA, ITR, MCA, Score Card, Risk Lens) |
| **Part 2: Bank Admin Underwriting** | 1:45 – 3:35 (110s) | ~230 words | **Screens 11–15** (Bank Login, Portfolio, Customer 360, Tampering Forensics, Network Lens, AI Decision, LOS/ULI/ONDC Modal) |
| **Part 3: Cloud Architecture & Close** | 3:35 – 3:55 (20s) | ~50 words | Architecture, Google Cloud Mumbai (`asia-south1`), Live Link |
| **Total Master Video** | **3:55 min** | **~530 words** | **100% of Screens Covered** |
