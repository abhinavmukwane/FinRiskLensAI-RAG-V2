using System.Globalization;
using FinRiskLensAI.Core.Models.Facts;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.ML.Features
{
    /// <summary>Extracts stability features from the Udyam Aadhaar API response.</summary>
    public class UdyamFeatureExtractor
    {
        public void Extract(string udyamJson, MsmeFeatureSet features)
        {
            if (string.IsNullOrWhiteSpace(udyamJson)) return;

            var root = JObject.Parse(udyamJson);
            var main = root["main_details"] as JObject;
            if (main == null) return;

            features.HasUdyam = true;

            var incorporation = ParseDate(main.Value<string>("date_of_incorporation"))
                             ?? ParseDate(main.Value<string>("date_of_commencement"));
            if (incorporation.HasValue)
                features.BusinessVintageMonths = Math.Max(0,
                    (DateTime.UtcNow - incorporation.Value).TotalDays / 30.44);

            features.MajorActivity = main.Value<string>("major_activity") ?? string.Empty;

            // Latest classification wins (list is per classification year)
            var types = main["enterprise_type_list"] as JArray;
            var latest = types?
                .OfType<JObject>()
                .OrderByDescending(t => t.Value<string>("classification_year"))
                .FirstOrDefault();
            features.EnterpriseType = latest?.Value<string>("enterprise_type") ?? string.Empty;

            features.PlantLocationCount = (root["location_of_plant_details"] as JArray)?.Count ?? 0;
            features.NicCodeCount = (root["nic_code"] as JArray)?.Count ?? 0;

            // ── Industry risk from the primary NIC code, e.g. "61 - Telecommunications"
            var nic2Raw = (root["nic_code"] as JArray)?.OfType<JObject>()
                .Select(n => n.Value<string>("nic_2_digit"))
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            if (nic2Raw != null)
            {
                var dash = nic2Raw.IndexOf('-');
                features.SectorNic2 = (dash > 0 ? nic2Raw[..dash] : nic2Raw).Trim();
                features.SectorName = dash > 0 ? nic2Raw[(dash + 1)..].Trim() : nic2Raw.Trim();
                features.SectorRiskWeight = SectorRiskWeight(features.SectorNic2);
            }
            else if (!string.IsNullOrEmpty(features.MajorActivity))
            {
                features.SectorName = features.MajorActivity;
            }
        }

        /// <summary>
        /// Static sector risk lookup by NIC 2-digit division (0..1, higher = more
        /// favourable outlook). Bank-style industry weights — tune with real
        /// portfolio data; this is a hackathon-grade static table.
        /// </summary>
        private static double SectorRiskWeight(string nic2)
        {
            if (!int.TryParse(nic2, out var code)) return 0.65;
            return code switch
            {
                >= 1 and <= 3 => 0.55,      // agriculture & allied
                >= 10 and <= 12 => 0.70,    // food/beverage manufacturing
                >= 13 and <= 18 => 0.60,    // textiles, apparel, leather, printing
                >= 19 and <= 25 => 0.65,    // chemicals, plastics, metals mfg
                >= 26 and <= 33 => 0.70,    // electronics, machinery, equipment mfg
                >= 35 and <= 39 => 0.65,    // utilities, waste
                >= 41 and <= 43 => 0.50,    // construction
                >= 45 and <= 47 => 0.65,    // trade (wholesale/retail)
                >= 49 and <= 53 => 0.60,    // transport & logistics
                55 or 56 => 0.50,           // hotels & restaurants
                >= 58 and <= 60 => 0.70,    // publishing, media
                61 => 0.75,                 // telecommunications
                62 or 63 => 0.85,           // IT & software services
                >= 64 and <= 66 => 0.70,    // financial services
                >= 69 and <= 75 => 0.75,    // professional/scientific services
                >= 77 and <= 82 => 0.65,    // admin & support services
                85 => 0.70,                 // education
                86 or 87 or 88 => 0.80,     // healthcare
                _ => 0.65
            };
        }

        private static DateTime? ParseDate(string? value)
            => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }
}
