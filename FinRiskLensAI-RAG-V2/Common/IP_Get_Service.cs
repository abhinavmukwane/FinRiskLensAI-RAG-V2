using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FinRiskLensAI.Common
{
    /// <summary>
    /// Common helper for resolving the calling client's IP address in ASP.NET
    /// Core. Prefers the first X-Forwarded-For hop (behind a proxy/CDN) and
    /// falls back to the connection's RemoteIpAddress.
    /// </summary>
    public static class IP_Get_Service
    {
        // One shared client for the dev public-IP lookup (avoids socket exhaustion).
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        public static string GetClientIPAddress(HttpContext context)
        {
            if (context == null) return string.Empty;

            var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
            {
                var addresses = forwarded.Split(',');
                if (addresses.Length > 0)
                    return addresses[0].Trim();
            }

            var remote = context.Connection.RemoteIpAddress;
            if (remote != null)
            {
                if (remote.IsIPv4MappedToIPv6)
                    remote = remote.MapToIPv4();
                return remote.ToString();
            }

            return string.Empty;
        }

        /// <summary>
        /// Same as <see cref="GetClientIPAddress"/>, but when running on localhost
        /// (::1 / 127.0.0.1) it fetches the machine's public IP from api.ipify.org
        /// so the IP Risk API has a routable address during development.
        /// Production requests (with a real remote/forwarded IP) skip the lookup.
        /// </summary>
        public static async Task<string> GetClientIPAddressAsync(HttpContext context)
        {
            var ip = GetClientIPAddress(context);

            if (string.IsNullOrWhiteSpace(ip) || ip == "::1" || ip == "127.0.0.1")
            {
                try
                {
                    var publicIp = (await _http.GetStringAsync("https://api.ipify.org")).Trim();
                    if (!string.IsNullOrWhiteSpace(publicIp))
                        return publicIp;
                }
                catch
                {
                    // Offline / blocked — fall back to the local address.
                }
            }

            return ip;
        }
    }
}
