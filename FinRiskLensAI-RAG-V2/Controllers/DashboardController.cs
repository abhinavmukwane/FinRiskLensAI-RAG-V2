using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Cases;
using FinRiskLensAI.Fraud.Pipeline;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Interfaces.IServices.GST;
using FinRiskLensAI.Core.Models.Mca;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Core.Models.Universal;
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
        private readonly IMsmeDataStore _store;
        private readonly ILogger<DashboardController> _logger;
        private readonly IAccountAggregatorService _aaService;
        private readonly IIpRiskService _ipRisk;
        private readonly IGSTR2And3BResponceService _gstResponces;
        private readonly IDummyDataService _dummyData;
        private readonly CustomerProfileBuilder _profile;
        private readonly ICaseService _cases;
        private readonly CaseQueue _queue;

        // Every file at the cap, plus room for the form fields
        private const long MaxUploadBytes = (long)CaseService.MaxFiles * CaseService.MaxFileBytes + 1024 * 1024;

        public DashboardController(IMsmeDataStore store, ILogger<DashboardController> logger, 
            IAccountAggregatorService AAService, IIpRiskService ipRisk, IGSTR2And3BResponceService gstResponces, 
            IDummyDataService dummyData, CustomerProfileBuilder profile, ICaseService cases, CaseQueue queue)
        {
            _cases = cases;
            _queue = queue;
            _store = store;
            _logger = logger;
            _aaService = AAService;
            _ipRisk = ipRisk;
            _gstResponces = gstResponces;
            _dummyData = dummyData;
            _profile = profile;
        }

        /// <summary>
        /// Source-IP security audit for the "Sign-in IP" popup. Reads
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

                return Json(new { status = true, url, message = "Consent request created. Approve it in the Account Aggregator tab to share your bank statements." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AA consent initiation failed for {Uan}", user.UdyamNumber);
                return Json(new { status = false, message = "Something went wrong while contacting the Account Aggregator." });
            }
        }

        /// <summary>
        /// The "Connect Your Data Sources" verification hub. ViewBag.ItrFetched / McaFetched tell
        /// the view whether to grey out those "Fetch Details" launchers — once itr.json
        /// / mca.json exist for this UAN there is nothing left to fetch until a real
        /// re-fetch flow exists.
        /// </summary>
        public async Task<IActionResult> CustDashboard(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber;
            var itrFetched = !string.IsNullOrWhiteSpace(uan) && await _store.ExistsAsync(uan, MsmeDataFiles.Itr, ct);
            var mcaFetched = !string.IsNullOrWhiteSpace(uan) && await _store.ExistsAsync(uan, MsmeDataFiles.Mca, ct);
            ViewBag.ItrFetched = itrFetched;
            ViewBag.McaFetched = mcaFetched;

            // MCA only exists for companies and LLPs. Show the card either way — the
            // customer should see the platform covers it — but offer the fetch only
            // when there is something to fetch.
            var org = (await _profile.GetUdyamAsync(uan))?.OrganizationType;
            ViewBag.McaOrganizationType = org == "-" ? null : org;
            ViewBag.McaApplicable = CustomerProfileBuilder.IsMcaRegistered(org);

            // What the customer has uploaded so far, and whether each is read yet — never the findings
            var assessment = string.IsNullOrWhiteSpace(uan)
                ? null
                : await _cases.GetByUanAsync(uan, ct);
            var uploads = assessment?.Application.Documents ?? new List<CaseDocument>();
            ViewBag.Uploads = uploads;

            // Determine if data is available to generate scores and check risk
            bool hasData = await CheckHasDataAsync(uan, assessment, itrFetched, mcaFetched, ct);
            ViewBag.HasData = hasData;

            return View();
        }

        /// <summary>
        /// Customer's Risk Lens — evidence-based fraud detection assessment and status.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> RiskLens(CancellationToken ct)
        {
            var uan = HttpContext.Session.GetCurrentUser()?.UdyamNumber;
            if (string.IsNullOrWhiteSpace(uan)) return RedirectToAction(nameof(CustDashboard));

            var hasData = await CheckHasDataAsync(uan, ct);
            if (!hasData)
            {
                TempData["InfoMessage"] = "Please connect at least one data source or upload documents before viewing the Risk Lens.";
                return RedirectToAction(nameof(CustDashboard));
            }

            var assessment = await _cases.GetByUanAsync(uan, ct);
            return View(assessment);
        }

        /// <summary>
        /// Customer's Financial Health Card — 6-dimension credit health score (0-1000)
        /// and lending assessment.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> FinancialHealthCard(CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber;
            var enterpriseName = user?.NameOfEnterprise;
            if (string.IsNullOrWhiteSpace(uan)) return RedirectToAction(nameof(CustDashboard));

            var hasData = await CheckHasDataAsync(uan, ct);
            if (!hasData)
            {
                TempData["InfoMessage"] = "Please connect at least one data source or upload documents before viewing the Financial Score Card.";
                return RedirectToAction(nameof(CustDashboard));
            }

            var model = await _profile.GetFinancialHealthCardAsync(uan, enterpriseName, ct);
            return View(model);
        }

        /// <summary>
        /// Checks whether the MSME has any data available (documents, banking statement, ITR, MCA, or GST)
        /// to evaluate fraud risk or generate financial health score.
        /// </summary>
        private async Task<bool> CheckHasDataAsync(string? uan, CaseDetailViewModel? assessment, bool itrFetched, bool mcaFetched, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(uan)) return false;

            // 1. Uploaded documents, AA bank data, or findings in the loan application
            if (assessment != null && (assessment.Application.Documents.Count > 0
                || assessment.Application.BankDataAt != null
                || assessment.Findings.Count > 0))
            {
                return true;
            }

            // 2. Sources already fetched
            if (itrFetched || mcaFetched) return true;

            // 3. Stored source files in blob storage (excluding manifest, legacy result, and base udyam registration)
            var files = await _store.ListFilesAsync(uan, ct);
            if (files.Any(f => !f.Equals(MsmeDataFiles.Manifest, StringComparison.OrdinalIgnoreCase)
                            && !f.Equals(MsmeDataFiles.LegacyResult, StringComparison.OrdinalIgnoreCase)
                            && !f.Equals(MsmeDataFiles.Udyam, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 4. Stored GST responses in database
            var gst = await _gstResponces.GetResponcesByUan(uan);
            if (gst != null && (gst.GSTR2BResponseData != null || gst.GSTR3BResponseData != null))
            {
                return true;
            }

            return false;
        }

        private async Task<bool> CheckHasDataAsync(string? uan, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(uan)) return false;
            var assessment = await _cases.GetByUanAsync(uan, ct);
            var itrFetched = await _store.ExistsAsync(uan, MsmeDataFiles.Itr, ct);
            var mcaFetched = await _store.ExistsAsync(uan, MsmeDataFiles.Mca, ct);
            return await CheckHasDataAsync(uan, assessment, itrFetched, mcaFetched, ct);
        }

        /// <summary>
        /// A source just arrived: the customer's assessment runs again with it (opened first if this is
        /// the first thing they shared). Never fails the fetch — the data is stored either way.
        /// </summary>
        private async Task ReassessAsync(string uan, CancellationToken ct)
        {
            try
            {
                var id = await _cases.EnsureAssessmentAsync(uan, HttpContext.Session.GetCurrentUser()?.NameOfEnterprise, $"customer:{uan}", ct);
                _queue.Enqueue(id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Re-assessment after a source fetch failed for {Uan}", uan);
            }
        }

        /// <summary>
        /// The customer's own documents — statements of accounts not shared through Account Aggregator,
        /// ITR copies, GST returns, financials. They join the customer's one assessment and go through
        /// the same pipeline a bank upload does; the bank sees the result on its Risk Lens.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxUploadBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
        public async Task<IActionResult> UploadDocuments(List<IFormFile> files, CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            if (string.IsNullOrWhiteSpace(user?.UdyamNumber)) return RedirectToAction(nameof(CustDashboard));

            // Check sizes before buffering, so an oversize file is refused without being read
            var tooBig = files.FirstOrDefault(f => f.Length > CaseService.MaxFileBytes);
            if (tooBig != null)
            {
                TempData["UploadError"] = $"{tooBig.FileName} is over {CaseService.MaxFileBytes / 1024 / 1024} MB.";
                return RedirectToAction(nameof(CustDashboard));
            }

            var uploads = new List<CaseUpload>();
            foreach (var f in files)
            {
                using var ms = new MemoryStream((int)f.Length);
                await f.CopyToAsync(ms, ct);
                uploads.Add(new CaseUpload(f.FileName, ms.ToArray()));
            }

            var result = await _cases.AddCustomerDocumentsAsync(user.UdyamNumber, user.NameOfEnterprise, uploads,
                                                                $"customer:{user.UdyamNumber}", ct);
            if (result.Error != null)
            {
                TempData["UploadError"] = result.Error;
                return RedirectToAction(nameof(CustDashboard));
            }

            _logger.LogInformation("Customer {Uan} uploaded {Count} document(s) to assessment {Id}",
                user.UdyamNumber, uploads.Count, result.LoanApplicationID);
            _queue.Enqueue(result.LoanApplicationID!.Value);
            TempData["UploadDone"] = $"{uploads.Count} document{(uploads.Count == 1 ? "" : "s")} received — your bank will review them.";
            return RedirectToAction(nameof(CustDashboard));
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
        /// Seeds the logged-in MSME's blob folder with the DUMMY-DATA GST template,
        /// rewriting the template filer's GSTIN/PAN to the user's so the copied
        /// returns validate as theirs.
        /// <para>
        /// GST only — MCA and DIN are fetched separately from their own card on the
        /// dashboard (see <see cref="SeedMcaData"/>). Safe to call again: it copies
        /// only what is missing.
        /// </para>
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SeedFinancialData(CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber;
            if (string.IsNullOrWhiteSpace(uan) || string.IsNullOrWhiteSpace(user!.GstinNumber))
                return Json(new { status = false, message = "No Udyam number or GSTIN on your session. Sign in again." });

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

            _logger.LogInformation(
                "Seed GST {Uan}: {Copied} file(s) copied (already present: {GstPresent}).",
                uan, copied, gstPresent);

            if (gstError != null)
            {
                // The template detail is for the log, not the customer
                _logger.LogWarning("Seed GST {Uan}: {Error}", uan, gstError);
                return Json(new { status = false, copied, message = "GST returns aren't available right now. Try again later." });
            }

            if (copied > 0) await ReassessAsync(uan, ct);

            return Json(new
            {
                status = true,
                copied,
                message = copied > 0
                    ? "Sample GST returns loaded for your GSTIN (demo data)."
                    : "GST returns already loaded."
            });
        }

        /// <summary>
        /// Fetches the MSME's Corporate Affairs record: the MCA company response plus
        /// one DIN verification file per director, written into the MSME's blob folder
        /// as <c>mca.json</c> and <c>DIN_&lt;din&gt;.json</c>.
        /// <para>
        /// DIN files are derived from the directors listed in the MCA response, so MCA
        /// is always generated (or read back) first. Both parts guard on the file
        /// already existing, so calling this again only fills what is missing.
        /// </para>
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SeedMcaData(CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber;
            if (string.IsNullOrWhiteSpace(uan))
                return Json(new { status = false, message = "No Udyam number in session." });

            // Guard, not just a disabled button: seeding an MCA record for a firm or
            // proprietorship would invent a CIN and DINs that cannot exist, and that
            // fabricated data then flows into the bank portal and the credit report.
            var organizationType = (await _profile.GetUdyamAsync(uan))?.OrganizationType;
            if (!CustomerProfileBuilder.IsMcaRegistered(organizationType))
            {
                var label = string.IsNullOrWhiteSpace(organizationType) || organizationType == "-"
                    ? "a non-corporate entity" : organizationType;
                _logger.LogInformation("Seed MCA {Uan}: refused — organisation type {Org} is not on the MCA register.",
                    uan, organizationType ?? "(unknown)");
                return Json(new
                {
                    status = false,
                    notApplicable = true,
                    message = $"MCA records apply only to companies and LLPs. This enterprise is registered as "
                              + $"{label}, so it holds no CIN or DIN."
                });
            }

            try
            {
                // ── 1. MCA response — same company name as the profile, dynamic charges.
                string? mcaJson;
                var mcaCreated = false;

                if (!await _store.ExistsAsync(uan, MsmeDataFiles.Mca, ct))
                {
                    var companyName = user!.NameOfEnterprise;
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

                // ── 2. One DIN file per director listed on the MCA response.
                var dinCreated = 0;
                var directorCount = 0;

                if (!string.IsNullOrWhiteSpace(mcaJson))
                {
                    var directors = JsonConvert.DeserializeObject<McaResponseModel>(mcaJson)?
                        .message?.details?.directors ?? new List<McaResponseModel.McaDirectorModel>();
                    directorCount = directors.Count;

                    foreach (var d in directors)
                    {
                        var din = d.din_number?.Trim();
                        if (string.IsNullOrWhiteSpace(din)) continue;

                        var dinFile = MsmeDataFiles.DinFile(din);
                        if (await _store.ExistsAsync(uan, dinFile, ct)) continue;

                        var dinJson = _dummyData.GetDummyDin(din, d.director_name ?? string.Empty, user!.PanNumber);
                        await _store.UploadAsync(uan, dinFile, dinJson, ct);
                        dinCreated++;
                    }
                }

                _logger.LogInformation(
                    "Seed MCA {Uan}: MCA created {Mca}, {Directors} director(s), {Din} DIN file(s) created.",
                    uan, mcaCreated, directorCount, dinCreated);

                // Say what actually changed, so a repeat click is never a silent no-op.
                if (mcaCreated || dinCreated > 0) await ReassessAsync(uan, ct);

                var parts = new List<string>();
                parts.Add(mcaCreated ? "Sample MCA company record loaded (demo data)" : "MCA company record already loaded");
                if (dinCreated > 0) parts.Add($"{dinCreated} director record(s) loaded");
                else if (directorCount > 0) parts.Add("director records already loaded");

                return Json(new
                {
                    status = true,
                    mcaCreated,
                    dinCreated,
                    directorCount,
                    message = string.Join("; ", parts) + "."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCA seed failed for {Uan}", uan);
                return Json(new { status = false, message = "We couldn't load the MCA details. Try again." });
            }
        }

        // MCADetails moved to McaController (/Mca/MCADetails), backed by the
        // common IStaticResponseService over m_StaticResponces (MCAResponce/DINResponce).

        /// <summary>
        /// Fetches the MSME's ITR (Income Tax Return) record: a dummy response built
        /// from the MSME's own real onboarded identity (company name, PAN, GSTIN,
        /// email, mobile, registered address from Udyam) — only the financial figures
        /// inside are fabricated — written into the MSME's blob folder as
        /// <c>itr.json</c>. Triggered by the "Fetch ITR Details" button in the Income
        /// Tax Return Verification modal; the User ID/password fields there are a
        /// UI-only consent gate, like the GST/AA verification modals — nothing typed
        /// into them is ever sent to or read by the server.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> SeedItrData(CancellationToken ct)
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber;
            if (string.IsNullOrWhiteSpace(uan))
                return Json(new { status = false, message = "No Udyam number in session." });

            try
            {
                var alreadyPresent = await _store.ExistsAsync(uan, MsmeDataFiles.Itr, ct);
                if (!alreadyPresent)
                {
                    var udyam = await _profile.GetUdyamAsync(uan);
                    var aa = await _profile.GetAaAnalysisAsync(uan, ct);
                    var entityName = udyam?.HasData == true ? udyam.EnterpriseName : user!.NameOfEnterprise;
                    var constitution = udyam?.HasData == true ? udyam.OrganizationType : "PROPRIETORSHIP";

                    // UdyamDetailsViewModel only carries City/District/State/Pin — the actual
                    // street line (flat/building/road) lives solely in the raw Udyam JSON, so
                    // it's read directly here rather than fabricating a "Shop No." placeholder
                    // when the real registered address is already on file.
                    string? addressLine1 = null;
                    var udyamJson = await _store.DownloadAsync(uan, MsmeDataFiles.Udyam, ct);
                    if (udyamJson != null)
                    {
                        var main = JObject.Parse(udyamJson).SelectToken("main_details");
                        var parts = new[] { main?.Value<string>("flat"), main?.Value<string>("name_of_building"), main?.Value<string>("road") }
                            .Where(p => !string.IsNullOrWhiteSpace(p));
                        addressLine1 = parts.Any() ? string.Join(", ", parts) : null;
                    }

                    var itrJson = _dummyData.GetDummyItr(
                        uan: uan,
                        entityName: entityName ?? uan,
                        constitutionType: constitution ?? "PROPRIETORSHIP",
                        pan: user!.PanNumber ?? udyam?.Pan,
                        gstin: user.GstinNumber ?? udyam?.Gstin,
                        email: user.Email,
                        mobile: user.MobileNumber,
                        addressLine1: addressLine1,
                        city: udyam?.City,
                        state: udyam?.State,
                        pincode: udyam?.Pin,
                        aaAccounts: aa.HasData ? aa.Accounts : null);

                    await _store.UploadAsync(uan, MsmeDataFiles.Itr, itrJson, ct);
                }

                _logger.LogInformation("Seed ITR {Uan}: {Result}.", uan, alreadyPresent ? "already present" : "fetched");
                if (!alreadyPresent) await ReassessAsync(uan, ct);

                return Json(new
                {
                    status = true,
                    message = alreadyPresent ? "ITR details already loaded." : "Sample ITR details loaded (demo data)."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ITR seed failed for {Uan}", uan);
                return Json(new { status = false, message = "We couldn't load the ITR details. Try again." });
            }
        }
    }
}
