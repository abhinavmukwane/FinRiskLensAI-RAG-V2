using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Models.Mca;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.Universal;
using FinRiskLensAI.Models;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Corporate Affairs (MCA) screens for the logged-in MSME — company profile,
    /// directors (with DIN drill-down) and charges. Data comes from the cached
    /// responses in m_StaticResponces (MCAResponce / DINResponce) via the common
    /// IStaticResponseService, same pattern as the Udyam page. The UAN always
    /// comes from the authenticated session, never from the request.
    /// </summary>
    [CustDashboardAuthorize]
    public class McaController : Controller
    {
        private readonly IStaticResponseService _staticResponses;
        private readonly IMsmeDataStore _store;
        private readonly ILogger<McaController> _logger;

        private readonly CustomerProfileBuilder _profile;

        public McaController(IStaticResponseService staticResponses, IMsmeDataStore store,
            CustomerProfileBuilder profile, ILogger<McaController> logger)
        {
            _staticResponses = staticResponses;
            _store = store;
            _profile = profile;
            _logger = logger;
        }

        /// <summary>Corporate Intelligence Report page.</summary>
        [HttpGet]
        public async Task<IActionResult> MCADetails(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetMcaAsync(uan, ct));
        }

        /// <summary>
        /// Director profile for the DIN popup — called when the user clicks a
        /// director card. Reads DINResponce from m_StaticResponces; while that
        /// column has no data yet, falls back to the director's basics from the
        /// MCA response so the popup always renders.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDinDetail(string din, CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            var vm = new DinDetailViewModel { Din = din?.Trim() ?? "-" };

            if (string.IsNullOrWhiteSpace(uan) || string.IsNullOrWhiteSpace(din))
                return Json(new { status = false, data = vm });

            try
            {
                // Primary: the cached DIN verification response in m_StaticResponces.
                var dinJson = await _staticResponses.GetStaticCommonResponce(uan, StaticResponseType.Din);
                var profile = FindDinProfile(dinJson, din);

                // Secondary: the per-director DIN_<din>.json seeded into the MSME's
                // blob folder. That column holds one director; the blob has a file per
                // director, so it covers the rest of the board.
                if (profile == null)
                {
                    var blobJson = await _store.DownloadAsync(uan, MsmeDataFiles.DinFile(din), ct);
                    profile = FindDinProfile(blobJson, din);
                }

                if (profile != null)
                {
                    MapDinProfile(profile, vm);
                    vm.Found = true;
                    vm.DinVerified = true;
                }

                // Fallback / fill gaps: the director's basics from the MCA response.
                if (!vm.Found || vm.FullName == "-")
                {
                    var mcaJson = await _profile.LoadMcaJsonAsync(uan, ct);
                    var mca = string.IsNullOrWhiteSpace(mcaJson)
                        ? null
                        : JsonConvert.DeserializeObject<McaResponseModel>(mcaJson);

                    var director = mca?.message?.details?.directors?
                        .FirstOrDefault(d => string.Equals(d.din_number?.Trim(), din.Trim(), StringComparison.OrdinalIgnoreCase));

                    if (director != null)
                    {
                        vm.Found = true;
                        if (vm.FullName == "-") vm.FullName = director.director_name ?? "-";
                        if (vm.AppointedOn == "Not Available") vm.AppointedOn = FormatDate(director.start_date);
                        if (vm.DirectorSince == "Not Available") vm.DirectorSince = YearsSince(director.start_date);
                        if (vm.Companies == "Not Available") vm.Companies = mca?.message?.company_name ?? "Not Available";
                    }
                }

                vm.Initials = Initials(vm.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading DIN detail {Din} for {Uan}", din, uan);
            }

            return Json(new { status = vm.Found, data = vm });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Data access
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Locates the DIN entry matching <paramref name="din"/> inside the stored
        /// DINResponce payload — tolerant of a single object, a root array, or a
        /// data/directors envelope.
        /// </summary>
        private static DinResponseModel? FindDinProfile(string? dinJson, string din)
        {
            if (string.IsNullOrWhiteSpace(dinJson)) return null;

            try
            {
                var root = JToken.Parse(dinJson);
                var candidates = new List<JToken>();

                void Collect(JToken? t)
                {
                    if (t == null) return;
                    if (t.Type == JTokenType.Array) candidates.AddRange(t.Children());
                    else if (t.Type == JTokenType.Object) candidates.Add(t);
                }

                // "message" first: the MCA DIN API nests the whole director profile
                // there (full_name, father_name, dob, address, email), while the root
                // carries only rrn/din/status. Collecting root first would match on
                // the root's din and return an object with every profile field null.
                Collect(root.SelectToken("message"));
                Collect(root);
                Collect(root.SelectToken("data"));
                Collect(root.SelectToken("directors"));
                Collect(root.SelectToken("message.data"));
                Collect(root.SelectToken("message.details.directors"));

                var match = candidates.FirstOrDefault(c =>
                {
                    var candidateDin = c.Value<string>("din") ?? c.Value<string>("din_number");
                    return string.Equals(candidateDin?.Trim(), din.Trim(), StringComparison.OrdinalIgnoreCase);
                })
                // Single-director payloads may not repeat the DIN — accept the lone object.
                ?? (candidates.Count == 1 ? candidates[0] : null);

                return match?.ToObject<DinResponseModel>();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void MapDinProfile(DinResponseModel p, DinDetailViewModel vm)
        {
            // The API pads some name fields with leading spaces — trim for display.
            static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

            vm.Din = Clean(p.din) ?? Clean(p.din_number) ?? vm.Din;
            vm.FullName = Clean(p.full_name) ?? Clean(p.name) ?? "-";
            vm.FatherName = Clean(p.father_name) ?? vm.FatherName;
            vm.Dob = FormatDate(p.dob ?? p.date_of_birth, vm.Dob);
            vm.Nationality = Clean(p.nationality) ?? vm.Nationality;

            var pan = Clean(p.pan) ?? Clean(p.pan_number);
            vm.Pan = pan == null ? vm.Pan : Core.Common.Universal.MaskPan(pan);

            vm.Email = Clean(p.email) ?? Clean(p.email_id) ?? vm.Email;
            vm.PresentAddress = Clean(p.present_address) ?? vm.PresentAddress;
            vm.PermanentAddress = Clean(p.permanent_address) ?? vm.PermanentAddress;
            vm.AppointedOn = FormatDate(p.date_of_appointment ?? p.din_allocation_date, vm.AppointedOn);
            vm.DirectorSince = YearsSince(p.date_of_appointment ?? p.din_allocation_date, vm.DirectorSince);

            var companies = (p.companies ?? p.company_list ?? p.companies_associated)?
                .Select(c => c.company_name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();
            if (companies is { Count: > 0 })
                vm.Companies = string.Join(", ", companies);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Business-identity analysis (moved out of the view, Udyam-style)
        // ─────────────────────────────────────────────────────────────────

        internal static McaDetailsViewModel BuildAnalysis(McaResponseModel mca, string uan)
        {
            var info = mca.message!.details!.company_info ?? new McaResponseModel.McaCompanyInfoModel();
            var directors = mca.message.details.directors ?? new List<McaResponseModel.McaDirectorModel>();
            var charges = mca.message.details.charges ?? new List<McaResponseModel.McaChargeModel>();

            var model = new McaDetailsViewModel
            {
                HasData = true,
                Uan = uan,
                CompanyName = mca.message.company_name ?? "-",
                Cin = info.cin ?? mca.cin ?? "-",
                Status = info.company_status ?? "-",
                IsActive = string.Equals(info.company_status, "Active", StringComparison.OrdinalIgnoreCase),
                CompanyClass = info.class_of_company ?? "-",
                Category = info.company_category ?? "-",
                SubCategory = info.company_sub_category ?? "-",
                RocCode = info.roc_code ?? "-",
                RegistrationNumber = info.registration_number ?? "-",
                RegisteredAddress = info.registered_address ?? "-",
                Email = info.email_id ?? "-",
                Listed = string.Equals(info.listed_status, "Listed", StringComparison.OrdinalIgnoreCase) ? "Yes" : "No",
                AuthorizedCapitalText = FormatInr(ParseAmount(info.authorized_capital)),
                PaidUpCapitalText = FormatInr(ParseAmount(info.paid_up_capital)),
                Members = string.IsNullOrWhiteSpace(info.number_of_members) ? "-" : info.number_of_members,
                LastAgmDate = FormatDate(info.last_agm_date, "N/A"),
                LastBsDate = FormatDate(info.last_bs_date, "N/A"),
                HasCirp = !string.IsNullOrWhiteSpace(info.status_under_cirp),
                SuspendedText = string.IsNullOrWhiteSpace(info.suspended_at_stock_exchange) || info.suspended_at_stock_exchange.Trim() == "-"
                    ? "No" : "Yes"
            };
            model.CirpText = model.HasCirp ? info.status_under_cirp! : "No";

            if (DateTime.TryParse(info.date_of_incorporation, out var incorp))
            {
                model.IncorporationDate = incorp;
                model.IncorporationDateText = incorp.ToString("dd MMM yyyy");
            }

            // ── Directors ────────────────────────────────────────────────
            foreach (var d in directors)
            {
                var active = string.IsNullOrWhiteSpace(d.end_date) || d.end_date.StartsWith("1800");
                model.Directors.Add(new McaDetailsViewModel.McaDirectorItem
                {
                    Din = d.din_number ?? "-",
                    Name = d.director_name ?? "-",
                    Initials = Initials(d.director_name),
                    AppointedText = FormatDate(d.start_date),
                    IsActive = active
                });
            }

            // ── Charges (largest first) ──────────────────────────────────
            var parsed = charges
                .Select(c => new { c, amount = ParseAmount(c.charge_amount) })
                .OrderByDescending(x => x.amount)
                .ToList();
            var maxAmount = parsed.Count > 0 ? Math.Max(parsed[0].amount, 1) : 1;
            long openTotal = 0;

            foreach (var x in parsed)
            {
                var isOpen = string.Equals(x.c.status?.Trim(), "OPEN", StringComparison.OrdinalIgnoreCase);
                if (isOpen) { openTotal += x.amount; model.OpenChargeCount++; }

                var ratio = (double)x.amount / maxAmount;
                var heat = ratio >= 0.6 ? "heat-high" : ratio >= 0.25 ? "heat-medium" : "heat-low";

                DateTime.TryParse(x.c.date_of_creation, out var created);

                model.Charges.Add(new McaDetailsViewModel.McaChargeItem
                {
                    Asset = string.IsNullOrWhiteSpace(x.c.assets_under_charge) ? "Not specified" : x.c.assets_under_charge.Trim(),
                    Amount = x.amount,
                    AmountText = "₹" + x.amount.ToString("N0"),
                    AmountShort = FormatInr(x.amount),
                    CreatedText = FormatDate(x.c.date_of_creation),
                    ModifiedText = FormatDate(x.c.date_of_modification, "—"),
                    Status = isOpen ? "Open" : (x.c.status ?? "-"),
                    IsOpen = isOpen,
                    Year = created == default ? "-" : created.Year.ToString(),
                    HeatClass = heat,
                    IconCss = heat == "heat-high" ? "text-danger" : heat == "heat-medium" ? "text-warning" : ""
                });
            }

            model.TotalCharges = model.Charges.Count;
            model.TotalOpenAmountText = FormatInr(openTotal);
            model.MaxChargeText = parsed.Count > 0 ? FormatInr(parsed[0].amount) : "₹0";
            var years = model.Charges.Where(c => c.Year != "-").Select(c => int.Parse(c.Year)).ToList();
            model.OldestChargeYear = years.Count > 0 ? years.Min().ToString() : "-";
            model.LatestChargeYear = years.Count > 0 ? years.Max().ToString() : "-";

            var hasCompliance = model.LastAgmDate != "N/A" || model.LastBsDate != "N/A";

            // ── Corporate credibility score (transparent, rule-based) ─────
            int score = 0;
            score += model.IsActive ? 30 : 5;                                  // 1) company status
            score += model.HasCirp ? 0 : 15;                                   // 2) no insolvency
            score += model.Directors.Count >= 2 ? 15 : model.Directors.Count == 1 ? 8 : 0;  // 3) board
            if (model.RocCode != "-" && model.RegistrationNumber != "-") score += 10;       // 4) registration
            if (hasCompliance) score += 10;                                    // 5) filings present
            score += model.OpenChargeCount == 0 ? 20                           // 6) borrowing load
                   : openTotal <= 1_00_00_000 ? 15
                   : openTotal <= 10_00_00_000 ? 10 : 5;
            model.Score = Math.Min(score, 100);

            if (model.Score >= 85) { model.RiskLabel = "Low Risk"; model.RiskCss = "risk-low"; model.OverallRiskLabel = "LOW"; }
            else if (model.Score >= 65) { model.RiskLabel = "Moderate Risk"; model.RiskCss = "risk-medium"; model.OverallRiskLabel = "MODERATE"; }
            else { model.RiskLabel = "High Risk"; model.RiskCss = "risk-high"; model.OverallRiskLabel = "HIGH"; }

            // ── Risk breakdown (higher = healthier) ──────────────────────
            model.LegalPct = 60 + (model.IsActive ? 20 : 0) + (model.HasCirp ? 0 : 12);
            model.GovernancePct = 60 + Math.Min(model.Directors.Count, 2) * 14;
            model.ChargesPct = Math.Max(35, 95 - model.OpenChargeCount * 5
                - (openTotal > 10_00_00_000 ? 15 : openTotal > 1_00_00_000 ? 8 : 0));
            model.CompliancePct = hasCompliance ? 95 : 65;

            model.LegalDotStyle = RadarDotStyle(model.LegalPct, "top");
            model.ChargesDotStyle = RadarDotStyle(model.ChargesPct, "right");
            model.ComplianceDotStyle = RadarDotStyle(model.CompliancePct, "bottom");
            model.GovernanceDotStyle = RadarDotStyle(model.GovernancePct, "left");

            // ── Observations ─────────────────────────────────────────────
            var obs = model.Observations;
            obs.Add(model.IsActive
                ? new() { Text = "Company is Active" }
                : new() { Text = $"Company status: {model.Status}", CssClass = "obs-danger", Icon = "bi-x-circle-fill" });
            if (model.CompanyClass != "-")
                obs.Add(new() { Text = $"{model.CompanyClass} Limited Company" });
            if (model.RocCode != "-")
                obs.Add(new() { Text = $"Registered under {model.RocCode}" });
            obs.Add(model.HasCirp
                ? new() { Text = $"CIRP status: {model.CirpText}", CssClass = "obs-danger", Icon = "bi-exclamation-octagon-fill" }
                : new() { Text = "No CIRP proceedings found" });
            if (model.OpenChargeCount > 0)
                obs.Add(new() { Text = $"Company has active secured borrowings ({model.OpenChargeCount} open charges)", CssClass = "obs-observation", Icon = "bi-eye-fill" });
            obs.Add(new() { Text = "Proceed with GST & Banking assessment", CssClass = "obs-recommendation", Icon = "bi-lightbulb-fill" });

            // ── Director verification matrix ─────────────────────────────
            var v = model.VerificationItems;
            v.Add(new() { Label = "DIN Verified", Pass = model.Directors.Count > 0 && model.Directors.All(d => d.Din != "-") });
            v.Add(new() { Label = "Active DIN", Pass = model.Directors.Count > 0 && model.Directors.All(d => d.IsActive) });
            v.Add(new() { Label = "Appointment Dates", Pass = model.Directors.Count > 0 && model.Directors.All(d => d.AppointedText != "-") });
            v.Add(new() { Label = "Director Linked", Pass = model.Directors.Count > 0 });
            v.Add(new() { Label = "Company Email Present", Pass = model.Email != "-" });
            v.Add(new() { Label = "PAN Not Available", Pass = false });

            // ── Incorporation journey ────────────────────────────────────
            var j = model.JourneySteps;
            j.Add(new() { Title = "Company Incorporated", Sub = $"{model.IncorporationDateText} · {model.RocCode}" });
            if (model.Directors.Count > 0)
                j.Add(new() { Title = "Directors Appointed", Sub = $"{model.Directors.Count} director{(model.Directors.Count > 1 ? "s" : "")} added" });
            if (model.TotalCharges > 0)
            {
                j.Add(new() { Title = "First Charge Created", Sub = $"{model.OldestChargeYear} · Secured borrowing initiated", DotCss = "orange", Icon = "bi-exclamation" });
                if (model.Charges.Any(c => c.ModifiedText != "—"))
                    j.Add(new() { Title = "Charges Modified", Sub = $"{model.OldestChargeYear} – {model.LatestChargeYear} · Multiple modifications", DotCss = "orange", Icon = "bi-arrow-repeat" });
            }
            j.Add(new() { Title = $"Current Status: {model.Status}", Sub = "Today · Company operational", DotCss = "maroon", Icon = "bi-geo-alt-fill" });

            return model;
        }

        // ── Small helpers ────────────────────────────────────────────────

        private static long ParseAmount(string? value)
            => long.TryParse(value?.Trim(), out var n) ? n : 0;

        /// <summary>₹ formatting in Indian units: Cr / L / plain.</summary>
        private static string FormatInr(long amount)
        {
            if (amount >= 1_00_00_000) return "₹" + (amount / 1_00_00_000d).ToString("0.##") + " Cr";
            if (amount >= 1_00_000) return "₹" + (amount / 1_00_000d).ToString("0.##") + " L";
            return "₹" + amount.ToString("N0");
        }

        /// <summary>"1800-01-01" is the API's sentinel for "not set".</summary>
        private static string FormatDate(string? value, string fallback = "-")
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("1800")) return fallback;
            return DateTime.TryParse(value, out var d) ? d.ToString("dd MMM yyyy") : fallback;
        }

        private static string YearsSince(string? value, string fallback = "Not Available")
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("1800") || !DateTime.TryParse(value, out var d))
                return fallback;
            var years = (int)((DateTime.Today - d).TotalDays / 365.25);
            return years <= 0 ? "Under a year" : $"{years} Year{(years > 1 ? "s" : "")}";
        }

        private static string Initials(string? name)
        {
            var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            return parts.Length == 1
                ? parts[0][..1].ToUpperInvariant()
                : string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
        }

        /// <summary>
        /// Radar dot inline style — healthier axes sit closer to the centre.
        /// axis: top | right | bottom | left.
        /// </summary>
        private static string RadarDotStyle(int pct, string axis)
        {
            var color = pct >= 80 ? "var(--success-green)" : pct >= 60 ? "var(--warning-orange)" : "var(--danger-red)";
            var offset = pct >= 80 ? 18 : pct >= 60 ? 10 : 4;

            return axis switch
            {
                "top" => $"background:{color}; top:{offset}%; left:48%;",
                "right" => $"background:{color}; top:46%; right:{offset}%;",
                "bottom" => $"background:{color}; bottom:{offset}%; left:48%;",
                _ => $"background:{color}; top:46%; left:{offset}%;"
            };
        }
    }
}
