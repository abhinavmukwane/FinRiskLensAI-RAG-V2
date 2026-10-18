using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Core.Models.Itr;
using FinRiskLensAI.Core.Models.GST;
using FinRiskLensAI.Core.Models.Mca;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Scoring;

namespace FinRiskLensAI.Core.Models.Admin
{
    public class BankDashboardViewModel
    {
        public BankPortfolioStats Stats { get; set; } = new();
        public string? LoadError { get; set; }
    }

    public class BankCustomersViewModel
    {
        public BankCustomerQuery Query { get; set; } = new();
        public BankCustomerPage Page { get; set; } = new();
        public List<string> States { get; set; } = new();
        public string? LoadError { get; set; }
        /// <summary>Risk at a glance per Udyam number; a customer missing here has nothing assessed yet.</summary>
        public IReadOnlyDictionary<string, RiskSummary> Risk { get; set; } = new Dictionary<string, RiskSummary>();
    }

    /// <summary>
    /// Customer 360 — every screen the MSME sees about itself, assembled for a
    /// bank officer. Each section feeds the same shared partial the customer-facing
    /// page uses, so the two can never show different data.
    /// </summary>
    public class BankCustomerDetailViewModel
    {
        public string? Uan { get; set; }
        public BankCustomerRow? Customer { get; set; }
        public string? LoadError { get; set; }

        /// <summary>Which tab to open on load: risk | udyam | gst | itr | aa | mca | security.</summary>
        public string ActiveTab { get; set; } = "risk";

        /// <summary>The customer's assessment — findings, documents, AI investigation — or null before any upload.</summary>
        public CaseDetailViewModel? Risk { get; set; }

        /// <summary>The customer's 6-dimension Financial Health Card score.</summary>
        public FinancialHealthCardViewModel? Card { get; set; }

        /// <summary>The latest AI credit decision — shown on the Risk Lens once the customer is Clear.</summary>
        public CreditDecision? Decision { get; set; }

        public UdyamDetailsViewModel? Udyam { get; set; }
        public GSTAnalysisViewModel? Gst { get; set; }
        public McaDetailsViewModel? Mca { get; set; }
        public ItrDetailsViewModel? Itr { get; set; }
        /// <summary>Backs both AA panels — linked accounts and the deep analysis.</summary>
        public AaAnalysisResult? Aa { get; set; }
        public IpVerificationViewModel? IpAudit { get; set; }

        /// <summary>Latest loan-case push per channel (LOS / ULI / ONDC), for the header chips.</summary>
        public List<LoanCase.LoanCasePushSummary> Pushes { get; set; } = new();
        /// <summary>Channels with a real endpoint configured; the rest run Simulated.</summary>
        public List<string> LiveChannels { get; set; } = new();
    }
}
