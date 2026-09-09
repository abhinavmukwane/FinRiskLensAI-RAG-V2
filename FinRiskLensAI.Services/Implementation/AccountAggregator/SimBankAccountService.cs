using FinRiskLensAI.Core.Interfaces.ICommon;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FinRiskLensAI.Services.Implementation.AccountAggregator
{
    /// <summary>
    /// Seeds the Finfactor SimBanks sandbox with a current account for a newly
    /// onboarded MSME's mobile number. Without this the AA consent flow finds no
    /// accounts to link for that mobile, so the bank-statement leg of the score
    /// has nothing to pull.
    /// </summary>
    public class SimBankAccountService : ISimBankAccountService
    {
        // Sandbox endpoint, deliberately inline rather than in appsettings: it is a
        // fixed public simulator with no credentials, and moving it to config would
        // add a setting nobody ever changes.
        private const string Endpoint = "https://simbanks.finfactor.in/ConnectHub/AccountManagement/Account";

        private const string FipId = "dhanagarbank";

        private readonly IHttpClientHelper _http;
        private readonly ILogger<SimBankAccountService> _logger;

        public SimBankAccountService(IHttpClientHelper http, ILogger<SimBankAccountService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<bool> RegisterAccountAsync(string? mobileNumber, string? email = null,
                                                     CancellationToken ct = default)
        {
            var mobile = Digits(mobileNumber);
            if (mobile.Length != 10)
            {
                _logger.LogWarning("SimBanks: skipped — {Mobile} is not a 10-digit mobile number.",
                    mobileNumber ?? "(null)");
                return false;
            }

            // A statement window the simulator will actually have data for: the last
            // full year up to yesterday. Today is excluded because the day is still
            // in progress and the sandbox rejects a future end date.
            var endDate = DateTime.Today.AddDays(-1);
            var startDate = endDate.AddYears(-1);

            var payload = new
            {
                startDate = startDate.ToString("yyyy-MM-dd"),
                endDate = endDate.ToString("yyyy-MM-dd"),
                fipId = FipId,
                fiType = "DEPOSIT",
                accountType = "CURRENT",
                rebitSchemaVersion = "v2",
                identifiers = new
                {
                    MOBILE = mobile,
                    AADHAR = BuildAadhaar(mobile),
                    EMAIL = string.IsNullOrWhiteSpace(email) ? $"msme.{mobile}@finrisklens.demo" : email.Trim(),
                    DOB = BuildDob(mobile)
                }
            };

            var json = JsonConvert.SerializeObject(payload);

            try
            {
                var response = await _http.SendPostRequestFullResp(Endpoint, json,
                    new Dictionary<string, string> { { "Accept", "application/json" } });

                var body = response.Content == null ? "" : await response.Content.ReadAsStringAsync(ct);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "SimBanks: account created for {Mobile} ({Start} to {End}) — {Status}.",
                        mobile, payload.startDate, payload.endDate, (int)response.StatusCode);
                    return true;
                }

                _logger.LogWarning(
                    "SimBanks: account creation for {Mobile} returned {Status}. Body: {Body}",
                    mobile, (int)response.StatusCode, Truncate(body, 500));
                return false;
            }
            catch (Exception ex)
            {
                // Never let the sandbox break onboarding — the MSME is still
                // registered, it just has no simulated account to link yet.
                _logger.LogError(ex, "SimBanks: account creation failed for {Mobile}.", mobile);
                return false;
            }
        }

        // ── identifier helpers ───────────────────────────────────────────────
        // Derived from the mobile so a re-run for the same MSME sends the same
        // identifiers rather than a second, conflicting identity.

        /// <summary>12 digits, never starting 0 or 1 (UIDAI never issues those).</summary>
        private static string BuildAadhaar(string mobile)
        {
            var seed = mobile.GetHashCode() & 0x7FFFFFFF;
            var rng = new Random(seed);
            var digits = new char[12];
            digits[0] = (char)('2' + rng.Next(8));
            for (var i = 1; i < 12; i++) digits[i] = (char)('0' + rng.Next(10));
            return new string(digits);
        }

        /// <summary>A proprietor of plausible age — 35 to 60 as of today.</summary>
        private static string BuildDob(string mobile)
        {
            var rng = new Random((mobile.GetHashCode() & 0x7FFFFFFF) + 7);
            var year = DateTime.Today.Year - rng.Next(35, 61);
            var month = rng.Next(1, 13);
            var day = rng.Next(1, DateTime.DaysInMonth(year, month) + 1);
            return new DateTime(year, month, day).ToString("yyyy-MM-dd");
        }

        private static string Digits(string? value)
            => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
    }
}
