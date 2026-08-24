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

    public class BankCustomerDetailViewModel
    {
        public string? Uan { get; set; }
        public BankCustomerRow? Customer { get; set; }
        /// <summary>Feeds the shared _FinancialHealthCardBody partial.</summary>
        public FinancialHealthCardViewModel? Card { get; set; }
        public string? LoadError { get; set; }
    }
}
