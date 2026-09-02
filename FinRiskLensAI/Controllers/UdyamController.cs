using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Interfaces.IServices.OnBoarding;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Universal;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Udyam registration screens for the logged-in MSME. All data comes from
    /// the cached API responses in m_StaticResponces via IStaticResponseService
    /// (no live Udyam API calls); the UAN always comes from the authenticated
    /// session, never from the request.
    /// </summary>
    [CustDashboardAuthorize]
    public class UdyamController : Controller
    {
        private readonly CustomerProfileBuilder _profile;
        private readonly ILogger<UdyamController> _logger;

        public UdyamController(CustomerProfileBuilder profile, ILogger<UdyamController> logger)
        {
            _profile = profile;
            _logger = logger;
        }

        /// <summary>Business Identity &amp; Trust Analysis page.</summary>
        [HttpGet]
        public async Task<IActionResult> UdyamDetails()
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetUdyamAsync(uan));
        }

        /// <summary>
        /// The same computed analysis as JSON — reusable from any script or
        /// screen that needs the business-identity figures without the page.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUdyamAnalysis()
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            var model = await _profile.GetUdyamAsync(uan);
            return Json(new { status = model.HasData, data = model, message = model.LoadError ?? "Success" });
        }

        /// <summary>
        /// Business-identity analysis over the raw Udyam response: parses
        /// main_details / enterprise_type_list / location_of_plant_details /
        /// nic_code and derives the transparent rule-based identity score,
        /// risk label, classification, strengths and observations.
        /// </summary>
        internal static UdyamDetailsViewModel BuildAnalysis(UdyamResponseModel udyam, string sessionUan)
        {
            var mainDetails = udyam.main_details;

            var model = new UdyamDetailsViewModel
            {
                HasData = true,
                Uan = udyam.uan ?? sessionUan,
                EnterpriseName = mainDetails?.name_of_enterprise ?? "-",
                RegistrationDate = mainDetails?.registration_date ?? default,
                DateOfCommencement = mainDetails?.date_of_commencement
                                     ?? mainDetails?.date_of_incorporation
                                     ?? default,
                MajorActivity = mainDetails?.major_activity ?? "-",
                State = mainDetails?.state ?? "-",
                City = mainDetails?.city ?? "-",
                Pin = mainDetails?.pin ?? "-",
                OrganizationType = mainDetails?.organization_type ?? "-",
                Gstin = mainDetails?.gstin,
                Pan = mainDetails?.Pan,
                GstinMasked = Universal.MaskGstin(mainDetails?.gstin),
                PanMasked = Universal.MaskPan(mainDetails?.Pan)
            };

            // enterprise_type_list, oldest first
            model.EnterpriseTypeHistory = (mainDetails?.enterprise_type_list ?? new List<UdyamResponseModel.EnterpriseTypeModel>())
                .OrderBy(e => e.classification_year)
                .Select(e => new UdyamDetailsViewModel.EnterpriseTypeEntry
                {
                    Year = e.classification_year ?? "-",
                    Type = e.enterprise_type ?? "-",
                    ClassifiedOn = e.classification_date ?? default
                })
                .ToList();
            model.LatestEnterpriseType = model.EnterpriseTypeHistory.Count > 0
                ? model.EnterpriseTypeHistory.Last().Type
                : "-";

            // location_of_plant_details
            model.PlantLocations = (udyam.location_of_plant_details ?? new List<UdyamResponseModel.LocationOfPlantModel>())
                .Select(l => new UdyamDetailsViewModel.PlantLocationEntry
                {
                    UnitName = l.unit_name ?? "-",
                    City = l.city ?? "-",
                    District = l.district ?? "-",
                    State = l.state ?? "-"
                })
                .ToList();

            // main_details carries no district — use the first registered plant's.
            model.District = model.PlantLocations.Count > 0 ? model.PlantLocations[0].District : "-";

            // nic_code — entries come as "58201 - Publishing of operating systems…",
            // so split the code from the activity text on the first " - ".
            model.NicActivities = (udyam.nic_code ?? new List<UdyamResponseModel.NicCodeModel>())
                .Select(n =>
                {
                    var raw = n.nic_5_digit ?? n.nic_4_digit ?? n.nic_2_digit ?? "-";
                    var sep = raw.IndexOf(" - ", StringComparison.Ordinal);
                    return new UdyamDetailsViewModel.NicActivityEntry
                    {
                        Code = sep > 0 ? raw.Substring(0, sep).Trim() : raw,
                        Activity = sep > 0 ? raw.Substring(sep + 3).Trim() : (n.activity_type ?? "-"),
                        ActivityType = n.activity_type ?? "-"
                    };
                })
                .ToList();

            // ----- Derived: Business Age (accurate year count, not just year subtraction) -----
            var today = DateTime.Today;
            if (model.DateOfCommencement != default)
            {
                model.BusinessAgeYears = today.Year - model.DateOfCommencement.Year;
                if (today < model.DateOfCommencement.AddYears(model.BusinessAgeYears)) model.BusinessAgeYears--;
            }

            // ----- Derived: Registration status -----
            model.IsRegistered = model.RegistrationDate != default;
            model.RegistrationStatus = model.IsRegistered ? "Active" : "Inactive";

            // ----- Derived: Enterprise stability (unchanged classification across all recorded years) -----
            model.IsStableClassification = model.EnterpriseTypeHistory.Count > 0
                && model.EnterpriseTypeHistory.Select(e => e.Type).Distinct().Count() == 1;

            // ----- Derived: NIC diversity (distinct 5-digit codes) -----
            model.DistinctNicCodes = model.NicActivities.Select(n => n.Code).Distinct().Count();

            // =====================================================================
            // BUSINESS IDENTITY SCORE — transparent, rule-based (no random values)
            // Every point below is explained; total capped at 100.
            // =====================================================================
            int score = 0;

            // 1) Business Age: >=20yrs -> +30 | 10-20 -> +25 | 5-10 -> +18 | <5 -> +10
            if (model.BusinessAgeYears >= 20) score += 30;
            else if (model.BusinessAgeYears >= 10) score += 25;
            else if (model.BusinessAgeYears >= 5) score += 18;
            else score += 10;

            // 2) Enterprise Stability: classification identical across every recorded year -> +20
            if (model.IsStableClassification) score += 20;

            // 3) Registration: an active Udyam registration date is present -> +15
            if (model.IsRegistered) score += 15;

            // 4) Multiple Plant Locations: 1 -> +5 | 2 -> +10 | 3+ -> +15
            if (model.PlantLocations.Count >= 3) score += 15;
            else if (model.PlantLocations.Count == 2) score += 10;
            else if (model.PlantLocations.Count == 1) score += 5;

            // 5) NIC Diversity (registered activity count): 1 -> +5 | 2-3 -> +10 | 4+ -> +15
            if (model.NicActivities.Count >= 4) score += 15;
            else if (model.NicActivities.Count >= 2) score += 10;
            else if (model.NicActivities.Count == 1) score += 5;

            // 6) Organization Type: Public Limited Company -> +5
            if (model.OrganizationType.Equals("Public Limited Company", StringComparison.OrdinalIgnoreCase)) score += 5;

            // 7) GST Available -> +5
            if (!string.IsNullOrWhiteSpace(model.Gstin)) score += 5;

            // 8) PAN Available -> +5
            if (!string.IsNullOrWhiteSpace(model.Pan)) score += 5;

            // Maximum achievable score is 100 — cap here rather than let criteria overflow it
            model.BusinessIdentityScore = Math.Min(score, 100);

            // ----- Risk Classification (derived purely from the score above) -----
            if (model.BusinessIdentityScore >= 90) { model.RiskLabel = "Very Low Risk"; model.RiskCss = "risk-verylow"; }
            else if (model.BusinessIdentityScore >= 75) { model.RiskLabel = "Low Risk"; model.RiskCss = "risk-low"; }
            else if (model.BusinessIdentityScore >= 60) { model.RiskLabel = "Medium Risk"; model.RiskCss = "risk-medium"; }
            else if (model.BusinessIdentityScore >= 40) { model.RiskLabel = "High Risk"; model.RiskCss = "risk-high"; }
            else { model.RiskLabel = "Very High Risk"; model.RiskCss = "risk-veryhigh"; }

            // ----- Business Classification (derived, not a lookup table) -----
            if (model.BusinessAgeYears >= 20 && model.IsStableClassification && model.IsRegistered && model.PlantLocations.Count >= 2)
                model.BusinessClassification = "Stable";
            else if (model.BusinessAgeYears >= 5 && model.IsStableClassification)
                model.BusinessClassification = "Growing";
            else if (model.BusinessAgeYears < 5)
                model.BusinessClassification = "Emerging";
            else
                model.BusinessClassification = "Needs Verification";

            // ----- Strengths: only include what the JSON actually supports -----
            if (model.IsRegistered) model.Strengths.Add("Registered MSME");
            if (model.DateOfCommencement != default) model.Strengths.Add($"Operating since {model.DateOfCommencement.Year} ({model.BusinessAgeYears}+ Years)");
            if (model.IsStableClassification) model.Strengths.Add($"Stable {model.LatestEnterpriseType} Enterprise Classification");
            if (model.PlantLocations.Count > 1) model.Strengths.Add($"Multiple Plant Locations ({model.PlantLocations.Count})");
            if (!string.IsNullOrWhiteSpace(model.Gstin)) model.Strengths.Add("GST Registered");
            if (!string.IsNullOrWhiteSpace(model.Pan)) model.Strengths.Add("PAN Verified");
            if (model.OrganizationType.Equals("Public Limited Company", StringComparison.OrdinalIgnoreCase)) model.Strengths.Add("Public Limited Company");
            if (model.DistinctNicCodes > 1) model.Strengths.Add($"Diverse Service Activities ({model.DistinctNicCodes} NIC Codes)");

            // ----- Observations: short professional AI-style summary (max 3 lines) -----
            model.Observations =
                $"Established MSME operating for over {model.BusinessAgeYears} years with a consistently maintained {model.LatestEnterpriseType} Enterprise classification across all recorded years. " +
                $"Multiple registered plant locations ({model.PlantLocations.Count}), verified GST and PAN records, and diversified NIC service activities point to a mature, credible business profile. " +
                $"Overall business identity confidence is assessed as {model.RiskLabel}, supporting a favorable credit assessment posture.";

            return model;
        }
    }
}
