namespace FinRiskLensAI.Common
{
    /// <summary>
    /// What the public Product Guide (home page) knows about FinRiskLens AI: the curated answers behind
    /// the starter chips, and the fact sheet Gemini answers free questions from. Product facts only,
    /// never customer data. Wording follows CONTENT_GUIDELINES.md; every capability claim must match
    /// the code (§4b there): GST, ITR and MCA return sample data, the Udyam lookup reads stored registry
    /// responses, Account Aggregator runs against Finvu's test host. Change a claim here only when the
    /// code changes.
    /// </summary>
    public static class ProductGuideKnowledge
    {
        public const string StartRoute = "/Onboarding/CustOnboarding";

        public const string WelcomeTitle = "Meet Your AI Credit Guide";

        public const string Welcome =
            "FinRiskLens AI helps MSMEs and lenders understand business financial health from registration, tax, " +
            "banking, income-tax and corporate records. Ask how it works, or start an assessment.";

        /// <summary>The welcome card's suggested questions. Free questions: Gemini answers them, with curated fallbacks.</summary>
        public static readonly IReadOnlyList<string> SuggestedQuestions = new[]
        {
            "What is FinRiskLens AI?",
            "How does MSME credit risk assessment work?",
            "Which financial data sources are analyzed?",
            "How can I start an assessment?",
        };

        public const string OffTopic =
            "I'm here to explain FinRiskLens AI, its features and how our MSME credit assessment works. What would you like to explore?";

        public const string PrivateData =
            "For your privacy, please don't share PAN, GSTIN, Udyam or bank account numbers, OTPs or other personal details here. " +
            "This guide only explains the product and can't look up or analyze a business. To assess your business, start an " +
            "MSME assessment: you enter your details there, and each data source is shared only with your consent.";

        public const string Unavailable =
            "The AI guide is unavailable for a moment, so here is the closest answer from the product overview.";

        public sealed record Starter(string Id, string Label, string Icon);

        public static readonly IReadOnlyList<Starter> Starters = new[]
        {
            new Starter("features",  "Explore Product Features",            "bi-grid-1x2"),
            new Starter("analysis",  "Types of Analysis",                   "bi-bar-chart-line"),
            new Starter("msme",      "Benefits for MSMEs",                  "bi-shop"),
            new Starter("lenders",   "Benefits for Banks & NBFCs",          "bi-bank"),
            new Starter("how",       "How It Works",                        "bi-diagram-3"),
            new Starter("different", "What Makes FinRiskLens AI Different?", "bi-stars"),
            new Starter("start",     "How to Start an Assessment",          "bi-play-circle"),
        };

        public sealed class Card
        {
            public string Icon { get; set; } = "";
            public string Title { get; set; } = "";
            public string Text { get; set; } = "";
            public string? Tag { get; set; }
            // Types of Analysis only
            public string? Examines { get; set; }
            public string? WhyItMatters { get; set; }
            public string? Insight { get; set; }
            public string? Context { get; set; }
        }

        public sealed record Topic(string Id, string Title, string Answer, string? CardStyle, IReadOnlyList<Card> Cards,
                                   IReadOnlyList<string> FollowUps, bool ShowStartCta, string[] Keywords);

        private const string SampleTag = "Sample data in this demo";

        public static readonly IReadOnlyList<Topic> Topics = new[]
        {
            new Topic("features", "Explore Product Features",
                "FinRiskLens AI brings an MSME's records into one assessment. Each source is analyzed on its own, then the sources " +
                "are compared with each other, and every finding points to the evidence behind it.",
                "features",
                new[]
                {
                    new Card { Icon = "bi-building-check", Title = "Business Registration Analysis (Udyam)", Tag = "Stored registry record",
                        Text = "Udyam is India's government registry for MSMEs. Shows the business identity, Enterprise Classification, registration dates, Primary Business Activity and Registered Business Locations, with a rule-based registration profile score (0–100)." },
                    new Card { Icon = "bi-receipt", Title = "Tax & Sales Compliance (GST)", Tag = SampleTag,
                        Text = "Filing regularity, turnover trend, tax paid, Input Tax Credit (ITC) indicators and sales versus purchases, from the GST returns (GSTR-1, GSTR-3B, GSTR-2B)." },
                    new Card { Icon = "bi-bank", Title = "Consent-Based Banking Data (Account Aggregator)", Tag = "Consent flow · AA test environment",
                        Text = "Bank statements are shared only after the owner approves a consent request in an Account Aggregator. They support cash-flow analysis and a banking-health score (0–100) built from eight weighted components." },
                    new Card { Icon = "bi-file-earmark-text", Title = "Income Tax Filing (ITR)", Tag = SampleTag,
                        Text = "Declared income, tax computation, profitability, financial-statement items, key ratios and filing history from income-tax returns, with a rule-based score (0–100)." },
                    new Card { Icon = "bi-diagram-2", Title = "Corporate Registry (MCA)", Tag = SampleTag,
                        Text = "For companies and LLPs: company status, paid-up capital, directors, registered charges and filings, with a corporate credibility score (0–100)." },
                    new Card { Icon = "bi-intersect", Title = "Cross-Source Analysis", Tag = "Review signals, not verdicts",
                        Text = "Compares GST turnover with ITR turnover and bank credits, registered charges with turnover, and identity details across records. An uploaded bank statement is checked against the bank's own Account Aggregator record." },
                    new Card { Icon = "bi-speedometer2", Title = "Credit Risk Insights", Tag = "Rule-based scores",
                        Text = "Each source gets its own 0–100 score with the reasons behind it: Low Risk (85–100), Moderate (65–84), High Risk (below 65). There is no single combined score." },
                    new Card { Icon = "bi-patch-check", Title = "Evidence-Based Recommendations", Tag = "AI with human review",
                        Text = "Gemini investigates each customer; every finding cites the row, field, page or policy clause behind it, and claims that can't be traced are dropped. The credit officer confirms or dismisses each finding." },
                },
                new[] { "How does cross-source analysis work?", "How are the 0–100 scores calculated?", "How is AI kept accurate?" },
                false, new[] { "feature", "capabilit", "what can", "what does it do", "offer", "what is finrisk", "about finrisk" }),

            new Topic("analysis", "Types of Analysis",
                "Select an analysis to see what it examines, why it matters and what it helps you understand. Results depend on the " +
                "sources available and authorized for each assessment, and a discrepancy is a signal to review, not proof of fraud.",
                "analysis",
                new[]
                {
                    new Card { Icon = "bi-building-check", Title = "Business Identity & Registration",
                        Examines = "The Udyam record: legal name, Enterprise Classification, dates, Primary Business Activity (NIC code) and locations.",
                        WhyItMatters = "Confirms which business is being assessed before any figures are compared.",
                        Insight = "A registration profile score (0–100) with its reasons.",
                        Context = "GST, ITR and MCA records, to check the names and identifiers agree." },
                    new Card { Icon = "bi-receipt", Title = "GST Filing, Turnover & Tax Compliance",
                        Examines = "GST returns: filing regularity, monthly sales and purchases, tax paid, Input Tax Credit (ITC).",
                        WhyItMatters = "Regular filing and steady turnover indicate business continuity.",
                        Insight = "Turnover trend, filing gaps and ITC indicators.",
                        Context = "ITR turnover and bank credits, to see whether reported sales are consistent." },
                    new Card { Icon = "bi-bank", Title = "Banking & Cash Flow",
                        Examines = "Consented bank statements: inflows, outflows, balances, bounces, EMIs and transaction patterns.",
                        WhyItMatters = "Cash flow shows whether the business can service a loan.",
                        Insight = "A banking-health score (0–100) and flags such as pass-through, cash dependency and bounces.",
                        Context = "GST and ITR turnover, to compare bank credits with reported sales." },
                    new Card { Icon = "bi-file-earmark-text", Title = "Income Tax & Financial Performance",
                        Examines = "Income-tax returns: declared income, tax, profit, financial-statement items and ratios.",
                        WhyItMatters = "Shows profitability and how the business reports its income.",
                        Insight = "Filing history and financial ratios with a rule-based score (0–100).",
                        Context = "GST turnover, to compare what is reported for tax with sales returns." },
                    new Card { Icon = "bi-diagram-2", Title = "Corporate Registry, Directors & Charges",
                        Examines = "MCA records for companies and LLPs: status, directors, paid-up capital and registered charges.",
                        WhyItMatters = "Registered charges reveal existing secured borrowing; directors link related businesses.",
                        Insight = "A corporate credibility score (0–100) and director details.",
                        Context = "Turnover from GST or ITR, to put existing charges in proportion." },
                    new Card { Icon = "bi-intersect", Title = "Cross-Source Consistency",
                        Examines = "The same facts across sources: GST vs ITR turnover, bank credits vs reported sales, identity details across records.",
                        WhyItMatters = "Checking each document alone misses figures that don't agree with each other.",
                        Insight = "Ratios and differences with the periods compared, and where a source is missing.",
                        Context = "Uploaded documents, checked against the bank's own Account Aggregator record." },
                    new Card { Icon = "bi-speedometer2", Title = "Strengths, Weaknesses & Risk Indicators",
                        Examines = "The per-source scores, the reasons behind them and the findings raised.",
                        WhyItMatters = "Shows what supports the case and what needs attention.",
                        Insight = "Strengths, concerns and risk indicators, each tied to its source.",
                        Context = "Every additional source narrows the open questions." },
                    new Card { Icon = "bi-patch-check", Title = "Evidence-Based Recommendations",
                        Examines = "Findings with the row, field, page or policy clause behind each.",
                        WhyItMatters = "A credit officer can check any claim in one click instead of trusting an opinion.",
                        Insight = "Records needing further review and practical next steps.",
                        Context = "The lender's credit policy, which the AI cites when relevant." },
                },
                new[] { "How can GST turnover be compared with ITR turnover?", "How does banking data help understand cash flow?", "What can banks learn from corporate registry information?" },
                false, new[] { "type", "analys", "kind of", "examine" }),

            new Topic("msme", "Benefits for MSMEs",
                """
                ### Benefits for MSMEs
                Many MSMEs have little credit history but plenty of business records. FinRiskLens AI uses those records, with the owner's consent at every step.

                - **See your records in one structured view**: registration, tax, banking, income-tax and corporate information side by side.
                - **Find information gaps early**: missing or inconsistent records show up before a credit review, not during it.
                - **Understand strengths and concerns**: each source has a 0–100 score with the reasons behind it.
                - **Stay in control of your data**: bank statements are shared only after you approve an Account Aggregator consent request.
                """,
                null, Array.Empty<Card>(),
                new[] { "Which records do I need to start?", "How does consent-based data sharing work?", "What do the 0–100 scores mean?" },
                true, new[] { "msme", "owner", "my business", "small business", "entrepreneur", "loan" }),

            new Topic("lenders", "Benefits for Banks & NBFCs",
                """
                ### Benefits for Banks & NBFCs
                - **One page per customer**: the Risk Lens shows each source's 0–100 score and every finding with its evidence.
                - **Cross-source review**: figures are compared across GST, income-tax, banking and corporate records, so inconsistencies and missing evidence stand out.
                - **Explainable risk indicators**: each score lists the factors behind it; each AI finding cites a row, field, page or credit-policy clause.
                - **Human decision**: the credit officer confirms or dismisses every finding, and a confirmed finding blocks the loan case.
                - **Linked customers**: a shared director, contact, address or document across customers is flagged for every business involved.
                """,
                null, Array.Empty<Card>(),
                new[] { "How can cross-source analysis identify inconsistencies?", "What evidence can a credit analyst review?", "How does the platform explain risk indicators?" },
                false, new[] { "nbfc", "lender", "banks", "credit officer", "analyst", "underwrit" }),

            new Topic("how", "How It Works",
                """
                ### How It Works
                1. **Register**: the MSME enters its Udyam Registration Number, reviews the business details and verifies a contact with a one-time code.
                2. **Connect sources**: from the dashboard, the owner adds GST, income-tax and corporate records, gives Account Aggregator consent for bank statements, and can upload documents.
                3. **Check**: rule-based checks score each source (0–100); nine deterministic checks test uploaded documents, such as running balances and page totals.
                4. **Investigate**: a Gemini agent reviews the customer's data and the lender's policy. On the synthetic demo set this takes 12–43 seconds per customer.
                5. **Verify**: any AI claim that can't be traced to the customer's data is dropped, and the drop is shown.
                6. **Review**: the lender sees the Risk Lens and confirms or dismisses each finding.
                """,
                null, Array.Empty<Card>(),
                new[] { "What does the Gemini agent check?", "What are the nine deterministic checks?", "How do I start an assessment?" },
                true, new[] { "how it work", "how does it work", "process", "step", "pipeline", "workflow", "credit risk", "assessment work" }),

            new Topic("different", "What Makes FinRiskLens AI Different?",
                """
                ### What Makes FinRiskLens AI Different?
                - **Sources checked against each other**: not just each document on its own. An uploaded bank statement is compared with the bank's own Account Aggregator record.
                - **Evidence for every finding**: each finding points to the exact row, field, page or policy clause. AI claims without evidence are dropped.
                - **Arithmetic first, AI second**: deterministic checks run before AI judgment, so a clear error doesn't depend on a model's opinion.
                - **Scores with reasons**: each source has its own 0–100 score and explanation, rather than one opaque number.
                - **Built for new-to-credit MSMEs**: it uses the records a registered business already has, with consent, instead of a credit bureau file.
                """,
                null, Array.Empty<Card>(),
                new[] { "How is this different from checking each document separately?", "How is AI kept accurate?", "What are the benefits for banks and NBFCs?" },
                true, new[] { "differ", "unique", "why finrisk", "special", "compare to", "versus", "separately", "instead of", "accura", "trust", "hallucinat", "evidence" }),

            new Topic("start", "How to Start an Assessment",
                """
                ### How to Start an Assessment
                1. Select **Start MSME Assessment** below.
                2. Enter your **Udyam Registration Number** (from your Udyam certificate) on the assessment page, not in this chat.
                3. Review the business details and verify your contact with a 6-digit code. This step only verifies your contact; it doesn't give access to financial data.
                4. On your dashboard, connect GST, income-tax, banking and corporate records. Each source asks for its own consent.

                Lenders sign in from the **Sign In** menu at the top of the page.
                """,
                null, Array.Empty<Card>(),
                new[] { "Which records do I need to start?", "How does consent-based data sharing work?", "What happens after I connect my data?" },
                true, new[] { "start", "begin", "register", "sign up", "apply", "get started", "onboard", "assessment", "how do i" }),
        };

        public static Topic? FindTopic(string? id) =>
            Topics.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>The curated topic closest to a free question, for when Gemini is unavailable.</summary>
        public static Topic ClosestTopic(string question)
        {
            var q = question.ToLowerInvariant();
            return Topics.Select(t => (t, hits: t.Keywords.Count(k => q.Contains(k))))
                         .OrderByDescending(x => x.hits).First() is var best && best.hits > 0
                ? best.t
                : Topics[0];
        }

        /// <summary>
        /// The fact sheet Gemini answers from. Accurate to the code as of Oct 18; keep it in step with
        /// CONTENT_GUIDELINES.md §1 and §4b.
        /// </summary>
        public const string FactSheet = """
PRODUCT
- FinRiskLens AI: AI-powered credit intelligence and fraud detection for MSME lending in India. Built on Google Cloud with
  Gemini on Vertex AI for the Google AI Builder Cup 2026 (BFSI track) by team AS FinTech Developer.
- Audience: MSME owners (applicants) and lenders (banks, NBFCs, credit officers).
- Why it matters: an MSME's financial information is spread across separate records (registry, tax, bank, income tax,
  corporate). FinRiskLens AI brings the available records into one structured assessment, compares them, and shows where
  further verification is needed. Many MSMEs are new to credit, with little bureau history; FinRiskLens AI uses the records
  a registered business already has, with the owner's consent.

SOURCES (approved display names) AND THEIR STATUS IN THIS BUILD
- Business Registration (Udyam): India's government MSME registry. Shows identity, Enterprise Classification, registration
  and incorporation dates, Primary Business Activity (NIC = National Industrial Classification code), Registered Business
  Locations. Rule-based registration profile score 0–100 (not a creditworthiness score). Status: reads stored registry
  responses, not a live registry call.
- Tax & Sales Compliance (GST): Goods and Services Tax returns. GSTR-1 = Sales / Outward Supplies Return; GSTR-3B = Summary
  Tax Return; GSTR-2B = Input Tax Credit Statement; ITC = Input Tax Credit. Shows filing compliance, turnover and growth,
  ITC utilization, sales vs purchases, supplier concentration, tax paid directly in cash. Status: sample data in this demo;
  no live GST integration yet.
- Consent-Based Banking Data (Account Aggregator, AA): India's consent-based framework for sharing bank data. The owner
  approves a consent request; only then are statements shared. Supports cash-flow analysis: inflows, outflows, balances,
  bounces, EMIs, repayment capacity; banking-health score 0–100 from eight weighted components; risk flags such as
  pass-through, cash dependency and bounces. Status: real consent API (Finvu), configured against its test environment.
- Income Tax Filing (ITR): income-tax returns. Declared income, tax computation, profit, financial-statement items, key
  ratios, filing history; rule-based score 0–100. Status: sample data in this demo.
- Corporate Registry (MCA): India's Ministry of Corporate Affairs records, for companies and LLPs only. Company status,
  paid-up capital, directors (DIN = Director Identification Number), registered charges (secured borrowing), filings;
  corporate credibility score 0–100. Status: sample data in this demo. Director OFAC/AML screening is an illustrative demo,
  not a live check.
- Taxpayer / Business Identity (PAN): India's tax identity number.
- Uploaded documents (bank statements, salary slips, ITRs, KYC): real; they go through the fraud pipeline.
- Sign-in IP risk check (VPN, proxy, high-risk IP): exists; in this build it returns a stored response.

SCORES
- Per-source 0–100 scores, higher = lower risk. Bands: Low Risk (85–100), Moderate (65–84), High Risk (below 65).
- No combined or composite score exists: no Financial Health Score, no 0–1000 scale, no credit score, no CIBIL-like rating.
  Never describe one.

PIPELINE (how it works)
1 Ingest: uploads are typed from their bytes, hashed (SHA-256), PDF history read; stored in Cloud Storage, Mumbai.
2 Extract: Gemini 3.5 Flash reads every page in parallel and copies numbers exactly as printed, never corrected.
3 Check: nine deterministic checks in code: running balances, page carry-over, page totals, salary-slip arithmetic,
  round-tripping, window dressing, pass-through, document metadata, and the statement against the bank's own Account
  Aggregator record.
4 Link: across all customers, a shared director, mobile, email, address or document, or GST invoices going round in a
  loop, links customers for review.
5 Investigate: a Gemini 3.1 Pro agent works within one customer's data and the lender's credit policy and fraud typologies.
6 Verify: any AI claim that cites nothing in the customer's data, or quotes an amount it can't trace, is dropped, and the
  drop is shown.
7 Review: the credit officer sees the Risk Lens (one page per customer: each source's score and every finding with its
  evidence) and confirms or dismisses each finding; a confirmed finding blocks the loan case.
8 Decide: for a customer with no open findings, an AI credit decision within the capacity the credit policy allows; the
  loan case can go to the LOS, ULI or ONDC. This step is being completed for the demo.
- Fraud patterns covered: tampered statements, edited salary slips, undeclared EMIs, loan stacking, round-tripping,
  window dressing, pass-through (mule-like accounts), gambling and crypto exposure, linked applicants.

CROSS-SOURCE ANALYSIS
- The signed-in AI Assistant compares, where the sources exist: GST annualized turnover vs ITR turnover; GST turnover vs
  annualized bank credits; ITR turnover vs bank business inflow; MCA open registered charges vs ITR or GST turnover; the
  GST figure quoted on the ITR vs the GST returns; and whether names, PAN and GSTIN agree across records.
- An uploaded bank statement is compared with the bank's own Account Aggregator record.
- A discrepancy is a signal to review (it may have innocent explanations such as different periods, timing or non-business
  credits), never automatic proof of fraud. Results depend on which sources are available and authorized.

MEASURED (synthetic demo set only)
- AI investigation: 12–43 seconds per customer. Extraction: 99.8–100% of 1,298 statement rows read exactly in two runs.
  14 of 14 demo cases raise exactly the expected findings; 0 false alarms on genuine applicants. These are demo
  measurements, not production performance.

ASSISTANTS
- Signed-in customers have an AI Assistant on their dashboard that answers about their own assessment across sources.
- This public Product Guide only explains the product and never sees any customer's data.

GETTING STARTED
- MSME: "Start MSME Assessment" on the home page → enter the Udyam Registration Number → review business details →
  verify a contact with a 6-digit one-time code (contact verification only, not consent to financial data) → dashboard →
  connect GST, ITR, MCA, give Account Aggregator consent, upload documents. Each source has its own consent.
- Lenders sign in from the Sign In menu (Lender Sign In). The live demo uses synthetic customers only.

NOT CLAIMED (never say these)
- Guaranteed loan approval, guaranteed fraud detection, reduced defaults, faster approvals as a measured fact, regulatory
  approval or certification, RBI compliance, partnerships, customer testimonials, live GST/ITR/MCA/registry verification,
  UPI or EPFO data, credit bureau integration.
""";
    }
}
