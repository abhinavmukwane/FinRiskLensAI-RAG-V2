namespace FinRiskLensAI.Core.Models.Onboarding
{
    /// <summary>
    /// ViewModel for /Udyam/UdyamDetails. Fully computed by UdyamController
    /// from the cached Udyam API response in m_StaticResponces — the Razor
    /// view only renders these values, it holds no business logic.
    /// </summary>
    public class UdyamDetailsViewModel
    {
        public string? Uan { get; set; }
        public string? LoadError { get; set; }
        public bool HasData { get; set; }

        // ── Business identity (from main_details) ────────────────────────
        public string EnterpriseName { get; set; } = "-";
        public DateTime RegistrationDate { get; set; }
        public DateTime DateOfCommencement { get; set; }
        public string MajorActivity { get; set; } = "-";
        public string State { get; set; } = "-";
        public string District { get; set; } = "-";
        public string City { get; set; } = "-";
        public string Pin { get; set; } = "-";
        public string OrganizationType { get; set; } = "-";
        public string? Gstin { get; set; }
        public string? Pan { get; set; }

        /// <summary>Masked via Universal.MaskGstin / MaskPan — what the UI shows.</summary>
        public string? GstinMasked { get; set; }
        public string? PanMasked { get; set; }

        // ── Parsed collections ───────────────────────────────────────────
        public List<EnterpriseTypeEntry> EnterpriseTypeHistory { get; set; } = new();
        public List<PlantLocationEntry> PlantLocations { get; set; } = new();
        public List<NicActivityEntry> NicActivities { get; set; } = new();
        public string LatestEnterpriseType { get; set; } = "-";

        // ── Derived business-identity analysis ───────────────────────────
        public int BusinessAgeYears { get; set; }
        public bool IsRegistered { get; set; }
        public string RegistrationStatus { get; set; } = "Inactive";
        public bool IsStableClassification { get; set; }
        public int DistinctNicCodes { get; set; }
        public int BusinessIdentityScore { get; set; }
        public string RiskLabel { get; set; } = "-";
        public string RiskCss { get; set; } = "risk-medium";
        public string BusinessClassification { get; set; } = "-";
        public List<string> Strengths { get; set; } = new();
        public string Observations { get; set; } = string.Empty;

        public class EnterpriseTypeEntry
        {
            public string Year { get; set; } = "-";
            public string Type { get; set; } = "-";
            public DateTime ClassifiedOn { get; set; }
        }

        public class PlantLocationEntry
        {
            public string UnitName { get; set; } = "-";
            public string City { get; set; } = "-";
            public string District { get; set; } = "-";
            public string State { get; set; } = "-";
        }

        public class NicActivityEntry
        {
            public string Code { get; set; } = "-";
            public string Activity { get; set; } = "-";
            public string ActivityType { get; set; } = "-";
        }
    }
}
