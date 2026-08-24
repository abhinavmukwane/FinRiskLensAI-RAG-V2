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

        public DateTime OnboardedOn { get; set; }

        // ── From t_MsmeScoreSummary; null when the MSME has not been scored yet
        public double? OverallScore { get; set; }
        public string? ScoreBand { get; set; }
        public decimal? TotalIndicativeEligibility { get; set; }
        public string? TopProductName { get; set; }
        public bool IsAnomalous { get; set; }
        public DateTime? ComputedAt { get; set; }

        public bool IsScored => OverallScore.HasValue;
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
        public string? Band { get; set; }
        public string? State { get; set; }
        /// <summary>"scored" | "pending" | null (all).</summary>
        public string? Status { get; set; }
        /// <summary>score | name | onboarded | eligibility.</summary>
        public string SortBy { get; set; } = "onboarded";
        public bool SortDesc { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    /// <summary>Aggregates behind the portfolio dashboard KPIs and charts.</summary>
    public class BankPortfolioStats
    {
        // KPI tiles — all counts, so they stay meaningful at any portfolio size.
        // Deliberately no portfolio average or summed eligibility: averaging over a
        // handful of MSMEs says little, and summing *indicative* limits reads like
        // committed exposure when it is nothing of the sort.
        public int TotalOnboarded { get; set; }
        public int TotalScored { get; set; }
        public int PendingAnalysis => Math.Max(0, TotalOnboarded - TotalScored);
        /// <summary>Excellent + Good — applicants that clear the lending bar today.</summary>
        public int LendableCount { get; set; }
        /// <summary>Onboarded within the current calendar month.</summary>
        public int NewThisMonth { get; set; }
        public int HighRiskCount { get; set; }
        public int AnomalyCount { get; set; }

        // Charts
        public Dictionary<string, int> BandDistribution { get; set; } = new();
        public Dictionary<string, int> OnboardingTrend { get; set; } = new();   // "yyyy-MM" → count
        public Dictionary<string, int> ScoreHistogram { get; set; } = new();    // "0-200" → count
        public Dictionary<string, int> TopStates { get; set; } = new();
        public Dictionary<string, int> ActivityMix { get; set; } = new();
        public Dictionary<string, int> ProductMix { get; set; } = new();

        public List<BankCustomerRow> RecentCustomers { get; set; } = new();
    }
}
