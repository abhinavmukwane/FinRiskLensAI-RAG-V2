using FinRiskLensAI.Core.Models.Admin;

namespace FinRiskLensAI.Models
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

        /// <summary>Which tab to open on load: score | udyam | gst | mca | security.</summary>
        public string ActiveTab { get; set; } = "score";

        public FinancialHealthCardViewModel? Card { get; set; }
        public UdyamDetailsViewModel? Udyam { get; set; }
        public GSTAnalysisViewModel? Gst { get; set; }
        public McaDetailsViewModel? Mca { get; set; }
        public IpVerificationViewModel? IpAudit { get; set; }
    }
}
