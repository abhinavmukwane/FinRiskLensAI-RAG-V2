namespace FinRiskLensAI.Core.Models.Admin
{
    /// <summary>One onboarded MSME as shown in the bank portal customer list.</summary>
    public class BankCustomerRow
    {
        public int UserRegistrationID { get; set; }
        public int? MsmeEnquiryID { get; set; }

        public string Uan { get; set; } = string.Empty;
        public string EnterpriseName { get; set; } = string.Empty;
        public string? GstinNumber { get; set; }
        public string? PanNumber { get; set; }
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }

        public string? State { get; set; }
        public string? City { get; set; }
        public string? OrganizationType { get; set; }
        public string? MajorActivity { get; set; }
        public DateTime? DateOfIncorporation { get; set; }

        /// <summary>Client IP captured when the MSME registered — feeds the security audit.</summary>
        public string? IPAddress { get; set; }

        public DateTime OnboardedOn { get; set; }

        /// <summary>Most recent loan-case push in any channel; null when never raised.</summary>
        public LoanCase.LoanCasePushSummary? LastPush { get; set; }
    }

    /// <summary>A page of customer rows plus the total for the pager.</summary>
    public class BankCustomerPage
    {
        public List<BankCustomerRow> Rows { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    }

    /// <summary>Filter/sort/page criteria for the customer list.</summary>
    public class BankCustomerQuery
    {
        public string? Search { get; set; }
        public string? State { get; set; }
        /// <summary>name | onboarded.</summary>
        public string SortBy { get; set; } = "onboarded";
        public bool SortDesc { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    /// <summary>Aggregates behind the portfolio dashboard KPIs and charts.</summary>
    public class BankPortfolioStats
    {
        // KPI tiles — counts only. The fraud-portfolio figures (open findings, rings,
        // discarded AI findings) join here once the findings engine exists.
        public int TotalOnboarded { get; set; }
        /// <summary>Onboarded within the current calendar month.</summary>
        public int NewThisMonth { get; set; }

        // Charts
        public Dictionary<string, int> OnboardingTrend { get; set; } = new();   // "yyyy-MM" → count
        public Dictionary<string, int> TopStates { get; set; } = new();
        public Dictionary<string, int> ActivityMix { get; set; } = new();

        public List<BankCustomerRow> RecentCustomers { get; set; } = new();
    }
}
