namespace FinRiskLensAI.Common
{
    /// <summary>
    /// Adds the standard browser security headers to every response.
    /// <para>
    /// The Content-Security-Policy whitelists exactly the third-party origins this
    /// application loads — anything not listed here is blocked by the browser, so
    /// adding a new CDN or API means adding it below too.
    /// </para>
    /// </summary>
    public static class SecurityHeaders
    {
        // ── Third-party origins, grouped by what they serve ──────────────
        //  jsDelivr / cdnjs / unpkg : Bootstrap, Bootstrap Icons, Chart.js,
        //                             ApexCharts, Leaflet, Font Awesome
        //  Google Fonts             : stylesheet (googleapis) + font files (gstatic)
        //  Bhashini                 : the language-translation widget
        //  CARTO                    : basemap tiles for the IP-audit map
        //  lh3.googleusercontent    : marketing images on the landing page
        private const string Cdn = "https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com";
        private const string Bhashini = "https://translation-plugin.bhashini.co.in";
        private const string BhashiniApi = "https://bhashini.gov.in https://api.finrisklensai.com";

        /// <summary>
        /// Builds the CSP. 'unsafe-inline' is required for both scripts and styles:
        /// the Razor views carry inline &lt;script&gt; blocks and inline style=""
        /// attributes throughout. That weakens CSP's XSS protection — the upgrade
        /// path is to move inline code into files and switch to per-request nonces.
        /// </summary>
        /// <param name="isDevelopment">
        /// When true, connect-src also allows the local dev tooling: Visual Studio
        /// Browser Link and hot reload open a WebSocket and poll over http on a
        /// random localhost port. Neither exists in a published build — ASP.NET Core
        /// injects them only in the Development environment — so production keeps
        /// the tighter policy.
        /// </param>
        private static string BuildCsp(bool isDevelopment)
        {
            // Source maps (e.g. bootstrap.min.css.map) are fetched, not linked, so
            // they fall under connect-src rather than style-src. Failing to load one
            // is harmless, but it logs a violation whenever DevTools is open — so the
            // CDNs are allowed here too. They already serve executable script, which
            // is the stronger grant.
            var connect = $"'self' {Cdn} {Bhashini} {BhashiniApi}";

            if (isDevelopment)
                connect += " ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*";

            return string.Join("; ", new[]
            {
                "default-src 'self'",
                $"script-src 'self' 'unsafe-inline' {Cdn} {Bhashini}",
                $"style-src 'self' 'unsafe-inline' https://fonts.googleapis.com {Cdn} {Bhashini}",
                $"font-src 'self' data: https://fonts.gstatic.com {Cdn}",
                $"img-src 'self' data: blob: https://lh3.googleusercontent.com https://*.basemaps.cartocdn.com {Cdn} {Bhashini}",
                $"connect-src {connect}",
                "frame-src 'self'",
                "object-src 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "frame-ancestors 'self'",
                "upgrade-insecure-requests"
            });
        }

        /// <summary>
        /// Registers the security-header middleware. Call this first in the
        /// pipeline so static files are covered too.
        /// <para>
        /// Set <c>Security:CspReportOnly</c> to true in configuration to emit the
        /// policy as Content-Security-Policy-Report-Only — the browser reports
        /// violations to the console without blocking anything, which is the safe
        /// way to validate the policy before enforcing it.
        /// </para>
        /// </summary>
        public static IApplicationBuilder UseSecurityHeaders(
            this IApplicationBuilder app, IConfiguration config, IWebHostEnvironment env)
        {
            var reportOnly = config.GetValue("Security:CspReportOnly", false);
            var cspHeader = reportOnly
                ? "Content-Security-Policy-Report-Only"
                : "Content-Security-Policy";

            var csp = BuildCsp(env.IsDevelopment());

            return app.Use(async (context, next) =>
            {
                // Set on OnStarting so the headers land even when a later
                // middleware short-circuits the request.
                context.Response.OnStarting(() =>
                {
                    var h = context.Response.Headers;

                    // Stop the browser MIME-sniffing a response into something executable.
                    h["X-Content-Type-Options"] = "nosniff";

                    // Clickjacking. frame-ancestors in the CSP is the modern control;
                    // this stays for older browsers that ignore it.
                    h["X-Frame-Options"] = "SAMEORIGIN";

                    // Send the full URL only to ourselves; cross-origin gets the origin only.
                    h["Referrer-Policy"] = "strict-origin-when-cross-origin";

                    // Switch off browser features this app never uses.
                    h["Permissions-Policy"] =
                        "accelerometer=(), autoplay=(), camera=(), display-capture=(), " +
                        "encrypted-media=(), fullscreen=(self), geolocation=(), gyroscope=(), " +
                        "magnetometer=(), microphone=(), midi=(), payment=(), usb=()";

                    h[cspHeader] = csp;

                    // Server fingerprinting — IIS/ASP.NET advertise themselves by default.
                    h.Remove("X-Powered-By");
                    h.Remove("X-Powered-By-Plesk");
                    h.Remove("Server");

                    return Task.CompletedTask;
                });

                await next();
            });
        }
    }
}
