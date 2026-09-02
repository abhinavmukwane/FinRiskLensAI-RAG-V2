using System.Collections.Generic;

namespace FinRiskLensAI.Core.Models.Mca
{
    /// <summary>
    /// ViewModel for /Mca/MCADetails — fully computed by McaController from the
    /// MCA response cached in m_StaticResponces.MCAResponce. The Razor view only
    /// renders these values; it holds no business logic.
    /// </summary>
    public class McaDetailsViewModel
    {
        public bool HasData { get; set; }
        public string? LoadError { get; set; }
        public string? Uan { get; set; }

        // ── Corporate profile ─────────────────────────────────────────────
        public string CompanyName { get; set; } = "-";
        public string Cin { get; set; } = "-";
        public string Status { get; set; } = "-";
        public bool IsActive { get; set; }
        public string CompanyClass { get; set; } = "-";
        public string Category { get; set; } = "-";
        public string SubCategory { get; set; } = "-";
        public string RocCode { get; set; } = "-";
        public string RegistrationNumber { get; set; } = "-";
        public string RegisteredAddress { get; set; } = "-";
        public string Email { get; set; } = "-";
        public string Listed { get; set; } = "-";
        public string AuthorizedCapitalText { get; set; } = "-";
        public string PaidUpCapitalText { get; set; } = "-";
        public string Members { get; set; } = "-";
        public DateTime? IncorporationDate { get; set; }
        public string IncorporationDateText { get; set; } = "-";
        public string LastAgmDate { get; set; } = "N/A";
        public string LastBsDate { get; set; } = "N/A";
        public string CirpText { get; set; } = "No";
        public bool HasCirp { get; set; }
        public string SuspendedText { get; set; } = "No";

        // ── Corporate credibility score ───────────────────────────────────
        public int Score { get; set; }
        public string RiskLabel { get; set; } = "-";
        public string RiskCss { get; set; } = "risk-medium";

        // ── Directors / charges ───────────────────────────────────────────
        public List<McaDirectorItem> Directors { get; set; } = new();
        public List<McaChargeItem> Charges { get; set; } = new();

        public int TotalCharges { get; set; }
        public int OpenChargeCount { get; set; }
        public string TotalOpenAmountText { get; set; } = "₹0";
        public string MaxChargeText { get; set; } = "₹0";
        public string OldestChargeYear { get; set; } = "-";
        public string LatestChargeYear { get; set; } = "-";

        // ── Derived panels ────────────────────────────────────────────────
        public List<McaObservation> Observations { get; set; } = new();
        public List<McaVerifItem> VerificationItems { get; set; } = new();
        public List<McaJourneyStep> JourneySteps { get; set; } = new();

        // ── Risk breakdown (0–100, higher = healthier) ────────────────────
        public int LegalPct { get; set; }
        public int GovernancePct { get; set; }
        public int ChargesPct { get; set; }
        public int CompliancePct { get; set; }
        public string OverallRiskLabel { get; set; } = "LOW";

        // Radar dot inline styles, precomputed per axis (position + color band)
        public string LegalDotStyle { get; set; } = "";
        public string GovernanceDotStyle { get; set; } = "";
        public string ChargesDotStyle { get; set; } = "";
        public string ComplianceDotStyle { get; set; } = "";

        public class McaDirectorItem
        {
            public string Din { get; set; } = "-";
            public string Name { get; set; } = "-";
            public string Initials { get; set; } = "?";
            public string AppointedText { get; set; } = "-";
            public bool IsActive { get; set; } = true;
        }

        public class McaChargeItem
        {
            public string Asset { get; set; } = "-";
            public long Amount { get; set; }
            public string AmountText { get; set; } = "₹0";
            public string AmountShort { get; set; } = "₹0";
            public string CreatedText { get; set; } = "-";
            public string ModifiedText { get; set; } = "—";
            public string Status { get; set; } = "-";
            public bool IsOpen { get; set; }
            public string Year { get; set; } = "-";
            /// <summary>heat-low | heat-medium | heat-high (relative to the largest charge).</summary>
            public string HeatClass { get; set; } = "heat-low";
            /// <summary>Accordion cash-icon tint by size: "", "text-warning", "text-danger".</summary>
            public string IconCss { get; set; } = "";
        }

        public class McaObservation
        {
            public string Text { get; set; } = "";
            /// <summary>"" (success) | obs-observation | obs-risk | obs-recommendation | obs-danger.</summary>
            public string CssClass { get; set; } = "";
            public string Icon { get; set; } = "bi-check-circle-fill";
        }

        public class McaVerifItem
        {
            public string Label { get; set; } = "";
            public bool Pass { get; set; }
        }

        public class McaJourneyStep
        {
            public string Title { get; set; } = "";
            public string Sub { get; set; } = "";
            /// <summary>"" (green) | orange | maroon — v-step-dot modifier.</summary>
            public string DotCss { get; set; } = "";
            public string Icon { get; set; } = "bi-check2";
        }
    }

    /// <summary>Director profile for the DIN popup, served by /Mca/GetDinDetail.</summary>
    public class DinDetailViewModel
    {
        public bool Found { get; set; }
        public string Din { get; set; } = "-";
        public string FullName { get; set; } = "-";
        public string Initials { get; set; } = "?";
        public string FatherName { get; set; } = "Not Available";
        public string Dob { get; set; } = "Not Available";
        public string Nationality { get; set; } = "Not Available";
        public string Pan { get; set; } = "Not Available";
        public string Email { get; set; } = "Not Available";
        public string PresentAddress { get; set; } = "Not Available";
        public string PermanentAddress { get; set; } = "Not Available";
        public string AppointedOn { get; set; } = "Not Available";
        public string DirectorSince { get; set; } = "Not Available";
        public string Companies { get; set; } = "Not Available";
        /// <summary>True when the profile came from a real DINResponce record.</summary>
        public bool DinVerified { get; set; }
    }
}
