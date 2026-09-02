using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Core.Models.Common
{
    /// <summary>
    /// Source-IP security audit shown in the dashboard popup, parsed from the
    /// IPResponce column of m_StaticResponces. The upstream IP/fraud API schema
    /// isn't fixed, so <see cref="FromJson"/> probes the common field names
    /// (IPQualityScore-style and generic geo-IP) and degrades gracefully.
    /// </summary>
    public class IpVerificationViewModel
    {
        public bool HasData { get; set; }

        public string Ip { get; set; } = "—";
        public int FraudScore { get; set; }

        public bool Vpn { get; set; }
        public bool Proxy { get; set; }
        public bool Tor { get; set; }
        public bool Bot { get; set; }

        public string Isp { get; set; } = "—";
        public string Organization { get; set; } = "—";
        public string Asn { get; set; } = "—";
        public string ConnectionType { get; set; } = "IPv4 Address Protocol";

        public string CountryName { get; set; } = "—";
        public string CountryCode { get; set; } = "";
        public string Region { get; set; } = "—";
        public string City { get; set; } = "—";
        public string? Latitude { get; set; }
        public string? Longitude { get; set; }

        /// <summary>Low fraud score AND no anonymising network → trusted origin.</summary>
        public bool IsSecure => HasData && FraudScore < 25 && !Vpn && !Proxy && !Tor && !Bot;

        public string? Coordinates =>
            (string.IsNullOrWhiteSpace(Latitude) || string.IsNullOrWhiteSpace(Longitude))
                ? null : $"{Latitude}, {Longitude}";

        /// <summary>
        /// Static placeholder used for display until a real IPResponce is stored
        /// in m_StaticResponces. Mirrors a clean, trusted local-network origin.
        /// </summary>
        public static IpVerificationViewModel Demo() => new()
        {
            HasData = true,
            Ip = "192.168.0.1",
            FraudScore = 0,
            Vpn = false,
            Proxy = false,
            Tor = false,
            Bot = false,
            Isp = "Localhost Private Network",
            Organization = "Development Environment",
            Asn = "Loopback (AS0)",
            ConnectionType = "IPv4 Address Protocol",
            CountryName = "India",
            CountryCode = "IN",
            Region = "Maharashtra",
            City = "Nagpur",
            Latitude = "21.1458",
            Longitude = "79.0882"
        };

        public static IpVerificationViewModel FromJson(string json)
        {
            var vm = new IpVerificationViewModel();
            if (string.IsNullOrWhiteSpace(json)) return vm;

            try
            {
                var root = JObject.Parse(json);

                // Real APIs often wrap the payload — accept bare or enveloped.
                var d = (root.SelectToken("response.message.data") as JObject)
                        ?? (root.SelectToken("data") as JObject)
                        ?? root;

                vm.Ip = Str(d, "ip", "ipAddress", "query", "host") ?? "—";
                vm.FraudScore = Int(d, "fraud_score", "fraudScore", "risk_score", "abuseConfidenceScore");

                vm.Vpn = Bool(d, "vpn", "active_vpn", "is_vpn");
                vm.Proxy = Bool(d, "proxy", "is_proxy");
                vm.Tor = Bool(d, "tor", "active_tor", "is_tor");
                vm.Bot = Bool(d, "bot_status", "is_crawler", "is_bot");

                vm.Isp = Str(d, "ISP", "isp") ?? "—";
                vm.Organization = Str(d, "organization", "org", "organisation") ?? vm.Isp;
                vm.Asn = Str(d, "ASN", "asn", "as") ?? "—";
                vm.ConnectionType = Str(d, "connection_type", "connectionType") ?? "IPv4 Address Protocol";

                vm.CountryName = Str(d, "country_name", "country", "countryName") ?? "—";
                vm.CountryCode = Str(d, "country_code", "countryCode", "country_code2") ?? "";
                vm.Region = Str(d, "region", "regionName", "region_name", "state") ?? "—";
                vm.City = Str(d, "city") ?? "—";
                vm.Latitude = Str(d, "latitude", "lat");
                vm.Longitude = Str(d, "longitude", "lon", "lng");

                // Consider it real data if we resolved at least an IP or a location.
                vm.HasData = vm.Ip != "—" || vm.City != "—" || vm.CountryName != "—";
            }
            catch
            {
                // Malformed payload — leave HasData=false so the UI shows the empty state.
            }

            return vm;
        }

        private static string? Str(JObject o, params string[] keys)
        {
            foreach (var k in keys)
            {
                var t = o[k] ?? o.SelectToken($"$..{k}");
                if (t != null && t.Type != JTokenType.Null)
                {
                    var s = t.Type == JTokenType.Object || t.Type == JTokenType.Array
                        ? null : t.ToString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            return null;
        }

        private static int Int(JObject o, params string[] keys)
        {
            var s = Str(o, keys);
            return int.TryParse(s, out var n) ? n : 0;
        }

        private static bool Bool(JObject o, params string[] keys)
        {
            var s = Str(o, keys);
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (bool.TryParse(s, out var b)) return b;
            return s.Equals("yes", System.StringComparison.OrdinalIgnoreCase)
                || s.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || s == "1";
        }
    }
}
