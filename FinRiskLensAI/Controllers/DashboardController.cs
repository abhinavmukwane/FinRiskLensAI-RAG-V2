using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Interfaces.IServices.GST;
using FinRiskLensAI.Core.Models.Mca;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.Universal;
using FinRiskLensAI.Models;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Controllers
{
    [CustDashboardAuthorize]
    public class DashboardController : Controller
    {
        private readonly IBlobAnalysisService _analysis;
        private readonly IMsmeDataStore _store;
        private readonly ILogger<DashboardController> _logger;
        private readonly IAccountAggregatorService _aaService;
        private readonly IIpRiskService _ipRisk;
        private readonly IGSTR2And3BResponceService _gstResponces;
        private readonly IDummyDataService _dummyData;
        private readonly CustomerProfileBuilder _profile;
        private readonly IEmailService _emailService;
        public DashboardController(IBlobAnalysisService analysis, IMsmeDataStore store, ILogger<DashboardController> logger, 
            IAccountAggregatorService AAService, IIpRiskService ipRisk, IGSTR2And3BResponceService gstResponces, 
            IDummyDataService dummyData, IEmailService emailService, CustomerProfileBuilder profile)
        {
            _analysis = analysis;
            _store = store;
            _logger = logger;
            _aaService = AAService;
            _ipRisk = ipRisk;
            _gstResponces = gstResponces;
            _dummyData = dummyData;
            _profile = profile;
            _emailService = emailService;
        }

        /// <summary>
        /// Source-IP security audit for the "Secure Connection IP" popup. Reads
        /// the client IP captured in session at login and hands it to the single
        /// IP risk service (currently sourced from m_StaticResponces.IPResponce;
        /// the Signzy API once the production key is live). Called only when the
        /// popup opens — never during login.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetIpVerificationDetail()
        {
            var user = HttpContext.Session.GetCurrentUser();
            var vm = await _profile.GetIpAuditAsync(user?.UdyamNumber, user?.ClientIP, HttpContext.RequestAborted);
            return Json(new { status = vm.HasData, data = vm });
        }

        /// <summary>
        /// Kicks off the Finvu AA consent journey for the logged-in MSME and
        /// returns the consent URL for the client to open in a new tab.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> InitiateAAFetch()
        {
            var user = HttpContext.Session.GetCurrentUser();
            if (user == null || string.IsNullOrWhiteSpace(user.UdyamNumber))
                return Json(new { status = false, message = "Your session has expired. Please log in again." });

            if (string.IsNullOrWhiteSpace(user.MobileNumber))
                return Json(new { status = false, message = "No mobile number is linked to your account." });

            var aaId = user.MobileNumber + "@finvu";
            try
            {
                var url = await _aaService.CreateConsentRequest(user.UdyamNumber, aaId);
                if (string.IsNullOrWhiteSpace(url))
                    return Json(new { status = false, message = "Could not initiate the Account Aggregator consent. Please try again." });

                return Json(new { status = true, url, message = "Consent request created — opening the Account Aggregator." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AA consent initiation failed for {Uan}", user.UdyamNumber);
                return Json(new { status = false, message = "Something went wrong while contacting the Account Aggregator." });
            }
        }

        public IActionResult CustDashboard()
        {
            return View();
        }

        /// <summary>
        /// GST Analysis page — the dedicated screen behind the "GST Details"
        /// sidebar item. Loads the latest stored GSTR-2B/3B responses via
        /// IGSTR2And3BResponceService.GetResponces(); the UdyamNumber is read
        /// from the authenticated session inside the service, never from the UI.
        /// Phase 1: page + data only — the BI charts come in the next phase.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GSTAnalysis()
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber?.Trim();
            return View(await _profile.GetGstAsync(uan));
        }

        /// <summary>
        /// Seeds the logged-in MSME's blob folder: the DUMMY-DATA GST template
        /// (rewriting the template filer's GSTIN/PAN to the user's so the copied
        /// returns validate as theirs), the MCA response, and one DIN file per
        /// director.
        /// <para>
        /// Each of the three parts guards itself, so calling this again is safe and
        /// fills in only what is missing. That matters for MSMEs onboarded before
        /// the MCA/DIN step existed: they already have GST files, and an early
        /// return on "GST present" would leave them without MCA/DIN forever.
        /// </para>
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SeedFinancialData(CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber;
            if (string.IsNullOrWhiteSpace(uan) || string.IsNullOrWhiteSpace(user!.GstinNumber))
                return Json(new { status = false, message = "No Udyam/GSTIN in session." });

            var existing = await _store.ListFilesAsync(uan, ct);
            var gstPresent = existing.Any(f => f.StartsWith("gstr", StringComparison.OrdinalIgnoreCase));

            int copied = 0;
            string? gstError = null;

            // ── 1. GST returns — copied from the shared template, once.
            if (!gstPresent)
            {
                const string template = "dummy-data";
                var files = await _store.ListFilesAsync(template, ct);
                var probeFile = files.FirstOrDefault(f => f.StartsWith("gstr3b", StringComparison.OrdinalIgnoreCase));

                if (files.Count == 0)
                {
                    gstError = "DUMMY-DATA template folder is empty.";
                }
                else if (probeFile == null)
                {
                    gstError = "DUMMY-DATA template has no gstr3b file to read the filer's GSTIN from.";
                }
                else
                {
                    // Template filer's GSTIN (top-level of any gstr3b file); PAN is chars 2..11.
                    var probe = await _store.DownloadAsync(template, probeFile, ct);
                    var srcGstin = probe != null ? JObject.Parse(probe).SelectToken("$..gstin")?.Value<string>() : null;

                    if (string.IsNullOrEmpty(srcGstin) || srcGstin.Length < 15)
                    {
                        gstError = "Could not read template GSTIN.";
                    }
                    else
                    {
                        var srcPan = srcGstin.Substring(2, 10);
                        var userGstin = user.GstinNumber;
                        var userPan = userGstin.Length >= 15 ? userGstin.Substring(2, 10) : (user.PanNumber ?? srcPan);

                        foreach (var f in files)
                        {
                            var content = await _store.DownloadAsync(template, f, ct);
                            if (content == null) continue;
                            content = content.Replace(srcGstin, userGstin).Replace(srcPan, userPan);
                            await _store.UploadAsync(uan, f, content, ct);
                            copied++;
                        }
                    }
                }
            }

            // ── 2. MCA response — same company name as the profile, dynamic charges.
            string? mcaJson;
            var mcaCreated = false;
            if (!await _store.ExistsAsync(uan, MsmeDataFiles.Mca, ct))
            {
                var companyName = user.NameOfEnterprise;
                if (string.IsNullOrWhiteSpace(companyName))
                {
                    var udyamJson = await _store.DownloadAsync(uan, MsmeDataFiles.Udyam, ct);
                    companyName = udyamJson != null
                        ? JObject.Parse(udyamJson).SelectToken("main_details.name_of_enterprise")?.Value<string>()
                        : uan;
                }
                mcaJson = _dummyData.GetDummyMca(uan, companyName ?? uan, user.PanNumber);
                await _store.UploadAsync(uan, MsmeDataFiles.Mca, mcaJson, ct);
                mcaCreated = true;
            }
            else
            {
                mcaJson = await _store.DownloadAsync(uan, MsmeDataFiles.Mca, ct);
            }

            // ── 3. DIN verification files — one per director from the MCA response,
            //      stored as DIN_<din>.json (e.g. DIN_85111678.json) so each director's
            //      profile can be pulled by DIN. Skips directors without a DIN and any
            //      file already present.
            var dinCreated = 0;
            if (!string.IsNullOrWhiteSpace(mcaJson))
            {
                var directors = JsonConvert.DeserializeObject<McaResponseModel>(mcaJson)?
                    .message?.details?.directors ?? new List<McaResponseModel.McaDirectorModel>();

                foreach (var d in directors)
                {
                    var din = d.din_number?.Trim();
                    if (string.IsNullOrWhiteSpace(din)) continue;

                    var dinFile = MsmeDataFiles.DinFile(din);
                    if (await _store.ExistsAsync(uan, dinFile, ct)) continue;

                    var dinJson = _dummyData.GetDummyDin(din, d.director_name ?? string.Empty, user.PanNumber);
                    await _store.UploadAsync(uan, dinFile, dinJson, ct);
                    dinCreated++;
                }
            }

            _logger.LogInformation(
                "Seed {Uan}: {Copied} GST file(s) copied (already present: {GstPresent}), MCA created: {Mca}, DIN files created: {Din}.",
                uan, copied, gstPresent, mcaCreated, dinCreated);

            // Report what actually changed, so a repeat click is not silently a no-op.
            var parts = new List<string>();
            if (copied > 0) parts.Add($"{copied} GST file(s) copied");
            else if (gstPresent) parts.Add("GST data already present");
            if (mcaCreated) parts.Add("MCA record created");
            if (dinCreated > 0) parts.Add($"{dinCreated} DIN record(s) created");
            if (gstError != null) parts.Add($"GST copy skipped — {gstError}");

            var nothingChanged = copied == 0 && !mcaCreated && dinCreated == 0;
            if (nothingChanged && gstError == null) parts.Add("nothing new to fetch");

            return Json(new
            {
                status = gstError == null,
                copied,
                mcaCreated,
                dinCreated,
                message = string.Join("; ", parts) + "."
            });
        }

        // MCADetails moved to McaController (/Mca/MCADetails), backed by the
        // common IStaticResponseService over m_StaticResponces (MCAResponce/DINResponce).

        /// <summary>
        /// Sends the "report ready" email for the logged-in MSME's current score.
        /// Called explicitly by the client — fetchGstrBtn (CustDashboard) and
        /// reAnalyzeBtn (Financial Health Card) — never from a GET/page load, so
        /// simply viewing or refreshing the card never sends an email. A no-op
        /// (status: false) when there's no score yet, e.g. right after fetching
        /// GSTR data but before the first analysis has run.
        /// <para>
        /// <paramref name="theme"/> comes from the caller's own
        /// localStorage.getItem('frl-theme') — that's the only reliable source
        /// for "what theme is this browser actually showing" (see theme.js);
        /// nothing server-side (session/cookie) tracks it.
        /// </para>
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendFinancialHealthReportEmail(string? theme, CancellationToken ct)
        {
            theme = string.Equals(theme, "theme2", StringComparison.OrdinalIgnoreCase) ? "theme2" : "theme1";

            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber?.Trim();
            if (string.IsNullOrWhiteSpace(uan))
                return Json(new { status = false, message = "No Udyam number on this account." });

            if (string.IsNullOrWhiteSpace(user!.Email))
            {
                _logger.LogWarning("Report-ready email skipped for {Uan} — no email on session user.", uan);
                return Json(new { status = false, message = "No email on file." });
            }

            try
            {
                var result = await _analysis.GetResultAsync(uan, ct);
                if (result == null)
                    return Json(new { status = false, message = "No score computed yet." });

                var udyamJson = await _store.DownloadAsync(uan, MsmeDataFiles.Udyam, ct);
                var enterpriseName = udyamJson != null
                    ? JObject.Parse(udyamJson).SelectToken("main_details.name_of_enterprise")?.Value<string>()
                    : null;

                string DimText(string name)
                {
                    var d = result.Dimensions.FirstOrDefault(x =>
                        string.Equals(x.Dimension, name, StringComparison.OrdinalIgnoreCase));
                    return d == null ? "N/A" : $"{d.Score:0}/{d.MaxPoints:0}";
                }

                var sent = await _emailService.SendReportReadyEmailAsync(
                    toEmail: user.Email,
                    recipientName: user.NameOfEnterprise,
                    businessName: enterpriseName ?? uan,
                    financialHealthScore: (int)Math.Round(result.OverallScore),
                    riskBand: result.ScoreBand.ToString(),
                    reportDate: result.ComputedAt.ToString("dd MMM yyyy"),
                    reportUrl: "",
                    metric1Label: "Revenue Vitality",
                    metric1Value: DimText("Revenue Vitality"),
                    metric2Label: "Cash Flow Health",
                    metric2Value: DimText("Cash Flow Health"),
                    metric3Label: "Compliance Quotient",
                    metric3Value: DimText("Compliance Quotient"),
                    theme: theme,
                    ct: ct);

                if (!sent)
                    _logger.LogWarning("Report-ready email failed to send for {Uan}.", uan);

                return Json(new { status = sent, message = sent ? "Report email sent." : "Could not send the email." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending report-ready email for {Uan}", uan);
                return Json(new { status = false, message = "Something went wrong sending the email." });
            }
        }

        /// <summary>
        /// Financial Health Card — renders the ML risk analysis result for the
        /// logged-in MSME only. The UAN is taken from the authenticated session
        /// (never from the request), so a user can only ever see their own card.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> FinancialHealthCard(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber;
            var model = new FinancialHealthCardViewModel { Uan = uan?.Trim() };

            if (string.IsNullOrWhiteSpace(model.Uan))
            {
                // Logged in but no Udyam number on the account yet — nothing to show.
                model.LoadError = "No Udyam number is linked to your account yet, so there is no Financial Health Card to display.";
                return View(model);
            }

            try
            {
                model.Status = await _analysis.GetStatusAsync(model.Uan, ct);
                model.Result = await _analysis.GetResultAsync(model.Uan, ct);

                // Enterprise name for the card header, straight from the stored Udyam payload
                var udyamJson = await _store.DownloadAsync(model.Uan, MsmeDataFiles.Udyam, ct);
                if (udyamJson != null)
                    model.EnterpriseName = JObject.Parse(udyamJson)
                        .SelectToken("main_details.name_of_enterprise")?.Value<string>();

                // Report-ready email is sent only from explicit user actions — see
                // SendFinancialHealthReportEmail, called by fetchGstrBtn/reAnalyzeBtn.
                // Loading or refreshing this page must never trigger it.
            }
            catch (OperationCanceledException)
            {
                // Blob read stalled/cancelled — typically a slow cold start (ML warmup)
                // saturating the box, or the client navigated away. Not a hard failure.
                _logger.LogWarning("Financial Health Card load canceled/timed out for {Uan} (likely a warming cold start).", model.Uan);
                model.LoadError = "Your Financial Health Card is still being prepared while the service warms up. Please refresh in a few seconds.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading Financial Health Card for {Uan}", model.Uan);
                model.LoadError = "Could not load your Financial Health Card right now. Please try again later.";
            }

            return View(model);
        }
    }
}
