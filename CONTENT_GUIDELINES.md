# FinRiskLens AI — Content Guidelines

Single source of truth for all user-facing text. Read this before changing copy on any page.

---

## 1. Product positioning

| Item | Standard wording |
|---|---|
| Product name | **FinRiskLens AI** (always this spelling — not "FinRiskLensAI" or "Finrisklens") |
| Theme | AI Builder Cup 2026 · BFSI: Intelligent Risk, Fraud & Financial Experiences |
| Category | AI-powered risk & fraud intelligence for MSME lending |
| One-line pitch | Verify business identity, check tax, banking and corporate records, and surface fraud findings backed by evidence. |
| Primary output | **Per-source risk checks** (0–100 scores with reasons) and **evidence-linked fraud findings** |
| Primary audience | MSME owners (applicants) and lenders / credit officers |

**What the product does (safe to claim):**
- Rule-based 0–100 scores for business identity (Udyam), income-tax filing health (ITR), banking health (Account Aggregator, eight weighted components) and corporate credibility (MCA).
- An IP fraud score (VPN, proxy and high-risk IP activity).
- Gemini AI fraud investigation: tampered statements, undeclared EMIs, loan stacking, round-tripping, pass-through, gambling/crypto exposure. Each finding cites the row, field, page or policy clause behind it.
- Grounding: AI claims that can't be traced to the case data are dropped and shown on the case page.
- Measured on the synthetic demo set: 12–43 s per case for AI investigation; 99.8–100% of 1,298 statement rows read exactly in two consecutive runs; 14 / 14 demo cases raise exactly the expected findings; 0 false alarms on genuine applicants.

**Not built: never claim.** A combined Financial Health Score, a 0–1000 scale, dimension weights, or a Financial Health Card. If a composite score is added back, update this section first.

**Tone:** clear, factual, calm. Write like a risk product, not an advertisement. Say "findings" and "checks". Say "score" only for the per-source 0–100 scores.

---

## 2. Terminology for an international audience (approved Oct 17)

Keep the Indian name recognizable and put it in brackets after a short English label. Judges and
bankers outside India should understand the term; Indian users should still recognize their document.

| Existing term | Approved user-facing display | Explanation (tooltips, helper text) |
|---|---|---|
| Udyam Registration | Business Registration (Udyam) | India's Government MSME Business Registry |
| GST | Tax & Sales Compliance (GST) | Indian Goods and Services Tax records |
| PAN | Taxpayer / Business Identity (PAN) | Indian tax identity identifier |
| Account Aggregator | Consent-Based Banking Data (AA) | Banking data shared with customer consent |
| ITR | Income Tax Filing (ITR) | Indian income-tax returns and filing records |
| MCA | Corporate Registry (MCA) | India's Ministry of Corporate Affairs records |
| Enterprise Type | Enterprise Classification | The recorded MSME enterprise category |
| Major Activity | Primary Business Activity | The business's recorded primary activity |
| Registered Plant Locations | Registered Business Locations | Locations listed in the Udyam record |

**Patterns**

- **Card and page titles:** the approved display, e.g. `Tax & Sales Compliance (GST)`. The explanation goes underneath as the subtitle or helper text.
- **Sidebar navigation** (280px wide, one line each): `Business Registration (Udyam)` is too long, so use the short forms
  `Registration (Udyam)` · `Tax & Sales (GST)` · `Banking Data (AA)` · `Income Tax (ITR)` · `Corporate Registry (MCA)`.
- **Identifiers keep their Indian names** because users must type or recognize them: `Udyam Registration Number`, `GSTIN`, `PAN`, `CIN`, `DIN`, `NIC code`. Explain them where useful (e.g. NIC = National Industrial Classification); never replace them with foreign equivalents.
- **Bare abbreviations** (AA, ITR, MCA) appear only in brackets, badges, tables and tooltips, once the full label is visible on the page.
- **Udyam page heading:** `AI-Powered Business Registration Analysis` (see §4).
- Older labels still in code (`Business Registry & Identity`, `Consent-Based Bank Data`, `Banking & Cash-Flow Analysis`) are replaced page by page as each page is updated. Don't use them in new text.

### 2a. Content rules (approved Oct 17)

1. Use the Indian source name with a short English explanation in brackets wherever it helps understanding.
2. Prefer concise, plain English suitable for bankers, MSME customers and international judges.
3. Use the same terminology across the application.
4. Keep Indian identifiers (GSTIN, PAN, Udyam Registration Number, NIC code). Explain them; don't replace them.
5. Distinguish data **listed** in a registry from data **independently verified** by another source.
6. Distinguish contact verification (one-time code) from consent to access financial data.
7. Never claim an integration, verification, score or security feature works unless the code supports it (§4b).
8. Keep the Financial Health Score (not built, §1) separate from the Udyam registration-profile score.
9. Don't hardcode customer names, registration numbers, scores, addresses or other dynamic values.
10. Keep text concise. Don't add long explanations to every card.
11. Preserve language switching (the Bhashini translation plugin).
12. This document is the single source of truth for user-facing terminology. Every page change follows it.

---

## 3. Standard terms

| Use | Avoid |
|---|---|
| Finding (with its evidence) | alert, flag (alone), "fraud detected" without evidence |
| [Source] score, e.g. "banking-health score (0–100)" | Financial Health Score, credit score, CIBIL-like score, rating |
| Risk check | dimension, pillar |
| Business / enterprise | company (unless it's MCA-registered), firm, shop |
| Assessment | application, evaluation journey |
| Consent | permission, approval (for data sharing) |
| Lender | bank (unless naming a specific bank) |
| Verify | validate, authenticate (in user-facing copy) |
| Analyze / analysis | (US spelling throughout — *analyze, behavior, center*) |
| Cash flow (noun), cash-flow (adjective) | cashflow |
| Sign in / Sign out | Login / Logout (as verbs) |

Risk bands for 0–100 scores, where higher means lower risk (exact wording; title case in body text, uppercase in badges):
**Low Risk (85–100) · Moderate (65–84) · High Risk (below 65)**. Individual source pages may keep their own finer labels, such as Very Low Risk.

---

## 4. Page titles and subtitles

Format: **Title** (2–6 words, title case) + **subtitle** (one sentence, ≤ 20 words, sentence case).

| Page | Title | Subtitle |
|---|---|---|
| Home | AI-Powered Credit Intelligence for MSMEs | Verify business identity and analyze tax, banking and corporate data to understand financial health and credit risk. |
| Onboarding — Udyam | Verify Business Identity | Enter the registration number shown on your Udyam certificate. |
| Business details review | Review Business Details | Check the details retrieved from the business registry before continuing. |
| OTP verification | Verify Contact | Enter the 6-digit code sent to [masked contact]. |
| Udyam analysis | AI-Powered Business Registration Analysis (customer page, approved Oct 17; the bank's Customer 360 keeps "Udyam Registration Profile") | Keep the existing line: registration profile score, rule-based, from the Udyam record (India's Government MSME Business Registry) only; not creditworthiness. "AI-Powered" refers to the assistant that answers questions about this record, never to the score. Labels: Business Registration (Udyam) on record · Udyam Registration Number · Enterprise Classification · Primary Business Activity · Registered Business Locations · GSTIN / PAN (listed on Udyam) · Business Registration (Udyam): On record (never a green "Registered": the record only carries a registration date) · GSTIN and PAN shown under Identified Strengths with an info icon and "· not checked", never a green check · NIC: India's National Industrial Classification. These labels are in the shared body, so Customer 360 shows them too. |
| GST analysis | Tax & Sales Compliance | Filing regularity, turnover trends and tax compliance from GST returns. |
| AA analysis | Banking & Cash-Flow Analysis | Balances, inflows, outflows and repayment capacity from consented bank data. |
| ITR analysis | Income Tax Filing | Declared income and filing history from income tax returns. |
| MCA analysis | Corporate Registry | Company status, directors and filings from the corporate registry. |
| Dashboard | Risk Overview | Per-source checks and fraud findings, each with its reasons and evidence. |
| Case page (lender) | Case Findings | What was found, where, and the evidence behind it. |

These are the defaults. A page may adjust its subtitle to match what it actually shows. It must never describe data the page doesn't show.

### Approved: Home page (`Home/Index.cshtml`)

| Element | Wording |
|---|---|
| Badge | Google AI Builder Cup 2026 · BFSI |
| Headline | AI-Powered Credit Intelligence **for MSMEs** (second line in the secondary colour) |
| Supporting text | Verify business identity and analyze tax, banking and corporate data to understand financial health and credit risk. |
| Primary / secondary CTA | Start MSME Assessment (`/Onboarding/CustOnboarding`) · How It Works (`#how-it-works`, the pipeline section). Lenders sign in from the header menu. |
| Hero visual | Animated mockup copied from the earlier FinRiskLens design: panel BANKING_HEALTH_CHECK · DEMO with a 0–100 dial cycling demo values (bands LOW RISK / MODERATE / HIGH RISK); floating cards BUSINESS REGISTRY · UDYAM — Identity ✓ and MONTHLY CASH FLOW |
| Bento section | Built for New-to-Credit MSMEs · Consented Data, Not a Bureau File · Findings in Seconds (AI Investigation, 12–43 s per case) · Explainable by Design (badges ACCOUNT AGGREGATOR, GROUNDED AI) · One case view for the lender (button Start Assessment) |
| Section order | Hero → Every claim traced to its evidence (pipeline: Ingest · Extract · Check · Link · Investigate · Verify · Review) → Built for New-to-Credit MSMEs (bento). The "Measured, not assumed" stats section was removed. |

**Hero-visual rule:** the mockup may look like a live AI app, but it shows a **per-source 0–100 score** (never a 0–1000 or composite Financial Health Score), and its bands must match §3. Keep "DEMO" in the panel label while the values are illustrative.

**Bento rule:** the earlier design's claims (five sources incl. UPI and EPFO, "under five seconds", "< 10 sec", IDBI LOS, ULI/OCEN, CGTMSE/MUDRA mapping, "the score adapts") are not built here. Reuse the layout and images, not those claims.

"Credit risk" in the supporting text refers to the per-source risk checks and fraud findings. It doesn't mean a composite credit score; see §1.

The browser title stays `FinRiskLens AI | Evidence-based fraud detection` (set in `_Layout.cshtml`, out of scope for the Home page step).

### Approved: customer onboarding (`CustOnboarding.cshtml`)

| Element | Wording |
|---|---|
| Progress rail | Business Registry (sub: Udyam number) · Review Business Details (sub: Confirm details) · Verify Contact (sub: One-time code) |
| Step 1 title | Verify Business Identity |
| Step 1 field / helper | Udyam Registration Number / Udyam is India's government registry for MSMEs. Enter the number shown on your Udyam certificate. |
| Step 1 button | Verify Business |
| Loader | Verifying business identity… · Checking registration number · Retrieving business registry record · Preparing details for review |
| Step 2 title | Review Business Details |
| Review cards | Business Details · Registration & Tax Details · Registered Address · Contact Verification |
| Review field labels | Legal Name · Legal Structure · Date of Incorporation · Enterprise Type · Udyam Registration Number · GSTIN · PAN · Email Address · Mobile Number |
| Address badge | ADDRESS ON RECORD (not "verified" unless a real check is performed) |
| Consent | I confirm these business details are correct and agree to receive a verification code by email. This step doesn't give access to my financial data. |
| Step 2 button | Send Code |
| Step 3 title / text | Verify Contact / Enter the 6-digit code sent to [masked contact] |
| Step 3 button / link | Verify & Continue / Resend code |
| Dev OTP modal | Your Verification Code / Use this code to continue: |
| Footer | Your data is shared only with your consent · FinRiskLens AI © 2026 |

**Runtime text (`wwwroot/FRLScripts/Onboarding/CustOnboarding.js`).** Keep it identical to the view wording above.

| Event | Wording |
|---|---|
| Invalid Udyam number | Enter a valid Udyam Registration Number (e.g. UDYAM-MH-01-0000001). |
| Details loaded (toast) | Business details retrieved. |
| Details failed to load | We couldn't load business details. Try again. |
| Udyam lookup failed (fallback) | We couldn't verify this registration number. Check it and try again. |
| Network error | Something went wrong. Try again in a moment. |
| Sending code (button) | Sending code… → back to Send Code |
| Resend | Sending… · A new code has been sent. · We couldn't resend the code. Try again. |
| Incomplete code | Enter all 6 digits. |
| Verifying (button) | Verifying… |

**Sign-in mode (`?mode=login`).** Sign In · Sign In to Your Dashboard · Udyam Registration Number or Mobile Number · Send Code · Verify Contact. Loader: Checking your details… · Finding your account · Checking registered contact · Sending verification code.

Server messages (`res.message` from controllers) are not covered yet for onboarding. Align them when those controllers are in scope.

### Approved: customer dashboard (`Dashboard/CustDashboard.cshtml`)

| Element | Wording |
|---|---|
| Banner | Welcome back · [business name from session] · **Sign-in IP:** [IP] (tooltip: IP address used to sign in. Select to see the IP risk check.) |
| Panel title / sub | Connect Your Data Sources / Each source is checked separately. Your bank reviews the results. |
| Source cards | Title = approved display, subtitle = explanation: Tax & Sales Compliance (GST) — Indian Goods and Services Tax records · Consent-Based Banking Data (AA) — Banking data shared with your consent · Income Tax Filing (ITR) — Indian income-tax returns and filing records · Corporate Registry (MCA) — India's Ministry of Corporate Affairs records |
| Card buttons | Fetch Details · Retrieved (done) · Not Applicable (MCA for non-companies) |
| Side panels | Titles use the short forms: Tax & Sales (GST) · Banking Data (AA) · Income Tax (ITR) · Corporate Registry (MCA). Text: Filing regularity, turnover and tax paid, from GSTR-2B and GSTR-3B returns. · Bank statements for cash-flow analysis, shared only after you approve the consent request in the Account Aggregator. · Declared income and filing history from income-tax returns. · Company status, paid-up capital, registered charges and directors. Companies and LLPs only. |
| AA consent | I agree to receive an Account Aggregator consent request. My bank statements are shared only after I approve that request in the Account Aggregator. Button: Request Consent |
| GST / ITR / MCA consent | I authorize FinRiskLens AI to retrieve my [source] to assess [what it shows] for my bank / for this credit assessment. |
| Status (client) | Tick the consent box to continue. · Retrieving GST returns… · Retrieving ITR details… · Retrieving company and director records… · Creating consent request… · Something went wrong. Try again. |
| Status (server) | Sample GST returns loaded for your GSTIN (demo data). · Sample ITR details loaded (demo data). · Sample MCA company record loaded (demo data). · [Source] already loaded. · Consent request created. Approve it in the Account Aggregator tab to share your bank statements. |

**AA rule:** creating a consent request is not the same as sharing data. Never say bank data is "fetched", "connected" or "verified" until the Account Aggregator callback confirms it.

**Demo-data rule:** while GST, ITR and MCA return generated sample data, every success message must say so ("demo data"). Remove the label only when a live integration replaces the generator.

### Layouts

| Element | Wording |
|---|---|
| Header brand line (`.brand-sub`) | Main layout: Evidence-based fraud detection · Onboarding layout: MSME Onboarding |
| Browser title | Main: FinRiskLens AI \| Evidence-based fraud detection · Onboarding: FinRiskLens AI \| MSME Onboarding |
| Header sign-in link | Sign In (onboarding) · Sign In menu with **MSME Sign In** / **Lender Sign In** (main layout) |
| Customer sidebar (`_DashboardLayout.cshtml`) | Dashboard · Registration (Udyam) · Tax & Sales (GST) · Banking Data (AA) · Income Tax (ITR) · Corporate Registry (MCA) · Sign Out |
| Footer tagline | Explainable credit intelligence for MSMEs, built on consented business, tax and banking data. |

Sign-in labels are applied in both `_Layout.cshtml` and `_CustOnboardingLayout.cshtml`. The footer tagline is applied in `_CustOnboardingLayout.cshtml` only so far. The brand lines and browser titles are as set on `main`. Change them only with the team's agreement.

**Never use "Login", "Log in" or "Logout"** in visible text. Use Sign In and Sign Out everywhere: buttons, menus, page titles. URLs and route names such as `/Auth/CustLogin` stay unchanged.

---

## 4a. Dates and source explanations

- **Display dates as `14 Jun 2019`** (day, 3-letter month, year). Never show raw values such as `2019-06-14T00:00:00`. Format in the display layer and leave stored or submitted values unchanged.
- **Explain Udyam on first use** on any page an applicant sees: "Udyam is India's government registry for MSMEs." Use the same pattern for other Indian sources when an international reader needs it, e.g. "GST (India's goods and services tax)".

---

## 4b. Claims awaiting functional verification

Don't add new copy that relies on these until they are confirmed in code:

| Claim on the site | What the code does today |
|---|---|
| "Verifying business identity", "Registry verified" | The Udyam lookup reads stored registry responses from the local database (`StaticResponseModel`). It doesn't call a live registry. |
| Financial Health Score 0–1000 with six weighted dimensions | **Not built.** No composite scorer exists in this codebase; the README says the fork removed it. |
| "Scores in seconds", "< 10 sec" | **Not measured.** Use the measured figures instead (e.g. 12–43 s AI investigation per demo case). |
| "Declining consent is never treated as bad credit" | **Can't be checked.** There's no composite score to apply it to. |
| Email code delivery | Email sending is attempted, but a failure is ignored (the check is commented out). The success message is always "OTP sent successfully to your email." |
| Location / IP risk | The IP risk lookup exists (`IpRiskService`), but with `Signzy:UseApi = false` it returns a **stored** response, not a live check of the sign-in IP. "Verified On" in the popup shows the browser's current time. There is no geolocation check of the business address, so the badge says ADDRESS ON RECORD. |
| GST "Fetch Details" | **Sample data.** Copies a shared `dummy-data` template and rewrites the GSTIN/PAN to the customer's. |
| ITR "Fetch Details" | **Sample data.** `DummyDataService.GetDummyItr`: real identity fields, generated financial figures. |
| MCA "Fetch Details" | **Sample data.** `DummyDataService.GetDummyMca` / `GetDummyDin`. The companies-and-LLPs-only check is real. |
| GST / ITR password fields | Required by the form but **never sent to the server** (a UI-only gate). Don't describe them as portal authentication. |
| Account Aggregator | **Real API.** Finvu consent request via `InitiateAAFetch`, opened in a new tab. Configured against Finvu's test host. |
| Customer document upload | **Real.** Files join the customer's assessment and go through the fraud pipeline; the bank sees the results. |

---

## 5. Buttons and calls to action

- 1–3 words, title case, start with a verb.
- Use one primary action per view. Make secondary actions outline or link style.
- Don't end with punctuation, "Now", "Here" or exclamation marks.

| Action | Label |
|---|---|
| Start the flow | Start Assessment (Home hero: Start MSME Assessment) |
| Learn more | How It Works |
| Move forward | Continue |
| Go back | Back |
| Submit a form | Verify / Submit |
| Verify a registry number | Verify Business |
| Send OTP | Send Code |
| Resend OTP | Resend Code |
| Submit OTP | Verify & Continue |
| Data consent | Give Consent |
| Skip optional source | Skip for Now |
| View results | View Results |
| Download | Download Report |
| Retry after error | Try Again |

---

## 6. Field labels and helper text

- Labels are nouns in title case: `Udyam Registration Number`, `Email Address`, `Mobile Number`.
- Placeholders show the format, not instructions: `UDYAM-XX-00-0000000`.
- Helper text is one short sentence under the field, in sentence case, with no trailing period if it's a fragment.
- Mark optional fields with `(optional)`. Don't mark required fields with an asterisk alone.

| Field | Helper text |
|---|---|
| Udyam Registration Number | Found on your Udyam Registration Certificate |
| GSTIN | 15-character GST Identification Number |
| PAN | 10-character Permanent Account Number |
| Email OTP | Enter the 6-digit code sent to your email |

---

## 7. System messages

Keep every message to one sentence: say what happened and, if needed, what to do.

**Validation (inline, under the field)**

- `Enter your Udyam Registration Number.`
- `Enter a valid Udyam Registration Number (e.g. UDYAM-MH-01-0000001).`
- `Enter a valid email address.`
- `Enter the 6-digit code.`

**Errors**

- `We couldn't verify this registration number. Check it and try again.`
- `This code is incorrect or has expired. Request a new code.`
- `We couldn't load this data right now. Try again in a moment.`
- `Something went wrong. Try again or contact support.`

Never show stack traces, HTTP codes or internal service names to applicants.

**Loading**

- `Verifying business identity…`
- `Fetching tax and sales data…`
- `Analyzing banking data…`
- `Running risk and fraud checks…`

Use the ellipsis character (…). Use present tense with "-ing".

**Success**

- `Business verified.`
- `Email verified.`
- `Consent recorded.`
- `Your results are ready.`

**Empty / unavailable data**

- `No data available from this source.`
- `Consent not given — this source is not checked.` (never imply a penalty)

---

## 8. Writing rules

1. **Short.** Headings ≤ 8 words. Descriptions ≤ 2 sentences (about 30 words). No paragraphs longer than 3 lines on desktop.
2. **Factual.** Only describe integrations, data sources, metrics and timings the product actually supports. No partnership, regulatory-approval or certification claims unless they're confirmed. Don't label something "verified" unless the app actually performs that check. Don't cite regulators (RBI, etc.) unless compliance is confirmed.
3. **No hype.** Avoid: *revolutionary, seamless, cutting-edge, game-changing, unlock, supercharge, world-class, best-in-class*.
4. **Consistent numbers.** Per-source scores are always `0–100` (en dash). There are no dimension weights to show. Only state measured timings, with their context, e.g. "12–43 seconds per case on the demo set".
5. **Plain language.** Explain jargon on first use or replace it. Prefer "bank statements" over "FI data", and "tax returns" over "GSTR-3B" unless the detail is needed.
6. **Consent-first and accurate.** When mentioning banking or tax data, make clear it's shared with the owner's consent. Every consent checkbox must describe only what that step does. For example, contact verification consents to receiving a code. It doesn't authorize access to financial records. Each data source (banking, tax) gets its own consent wording at the step where it is requested.7. **Capitalisation.** Title case for titles, headings, buttons and labels. Sentence case for body, helper text and messages. UPPERCASE only for small badges and data labels already styled that way.
8. **Symbols.** Use `&` in headings and labels only. Write "and" in sentences. Use the Oxford comma only when it prevents ambiguity.
9. **Neutral voice.** Address the applicant as "you / your business". Refer to the product as "FinRiskLens AI", not "we", in feature descriptions.
10. **Don't break the UI.** When editing copy, change text nodes only. Never change IDs, classes, `onclick` handlers, routes, `data-*` attributes, or `dont-translate` / `translate="no"` markers.

---

## 9. Change process

For each page:

1. Read this file.
2. Inspect the page and its scripts so you know which text is bound to JavaScript.
3. Update text only, using the terms above.
4. If a page needs a term that isn't listed here, add it to this file in the same change.
