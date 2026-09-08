using FinRiskLensAI.Controllers;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Models.Itr;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Core.Models.GST;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.GST;
using FinRiskLensAI.Core.Interfaces.IServices.OnBoarding;
using FinRiskLensAI.Core.Models.Mca;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Core.Models.Universal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Common
{
    /// <summary>
    /// Builds an MSME's page view models <b>by UAN</b> — Udyam, MCA, GST and the
    /// source-IP audit.
    /// <para>
    /// The customer-facing controllers take the UAN from the authenticated session
    /// (an MSME may only ever see its own data); the bank portal takes it from the
    /// route after <see cref="Utility.BankAdminAuthorize"/> has run. Both then land
    /// here, so the two surfaces render identical data from one code path instead of
    /// a copy that can drift.
    /// </para>
    /// <para>
    /// This deliberately performs no authorisation of its own — the caller has
    /// already decided the UAN is one it is allowed to read.
    /// </para>
    /// </summary>
    public class CustomerProfileBuilder
    {
        private readonly IStaticResponseService _staticResponses;
        private readonly IOnboardingService _onboarding;
        private readonly IMsmeDataStore _store;
        private readonly IGSTR2And3BResponceService _gstResponces;
        private readonly IIpRiskService _ipRisk;
        private readonly IAaStatementAnalysisService _aaAnalysis;
        private readonly ILogger<CustomerProfileBuilder> _logger;

        public CustomerProfileBuilder(
            IStaticResponseService staticResponses,
            IOnboardingService onboarding,
            IMsmeDataStore store,
            IGSTR2And3BResponceService gstResponces,
            IIpRiskService ipRisk,
            IAaStatementAnalysisService aaAnalysis,
            ILogger<CustomerProfileBuilder> logger)
        {
            _staticResponses = staticResponses;
            _onboarding = onboarding;
            _store = store;
            _gstResponces = gstResponces;
            _ipRisk = ipRisk;
            _aaAnalysis = aaAnalysis;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────
        //  Account Aggregator — Bank Statement Deep Analysis
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Reads the AA statement already stored in blob storage (never re-calls the
        /// AA API) and runs the deterministic deep analysis over it.
        /// </summary>
        public async Task<AaAnalysisResult> GetAaAnalysisAsync(string? uan, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan))
                return new AaAnalysisResult { LoadError = "No Udyam number is linked to this account, so no bank statement can be loaded." };

            string? json;
            try
            {
                json = await _store.DownloadAsync(uan, MsmeDataFiles.Aa, ct);
            }
            catch (Exception ex)
            {
                // Never log the statement itself — only that the read failed.
                _logger.LogError(ex, "Failed reading AA statement from blob for {Uan}", uan);
                return new AaAnalysisResult { Uan = uan, LoadError = "Could not read the Account Aggregator statement right now. Please try again later." };
            }

            var result = _aaAnalysis.Analyze(json);
            result.Uan = uan;
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  Udyam — Business Identity & Trust Analysis
        // ─────────────────────────────────────────────────────────────
        public async Task<UdyamDetailsViewModel> GetUdyamAsync(string? uan)
        {
            var model = new UdyamDetailsViewModel { Uan = uan };

            if (string.IsNullOrWhiteSpace(uan))
            {
                model.LoadError = "No Udyam number is linked to this account, so there are no Udyam details to display.";
                return model;
            }

            try
            {
                var udyam = await _staticResponses.GetStaticCommonResponce<UdyamResponseModel>(uan, StaticResponseType.Udyam);

                if (udyam != null)
                    return UdyamController.BuildAnalysis(udyam, uan);

                // Nothing cached in m_StaticResponces — fall back to the raw payload
                // held against the Udyam enquiry row.
                var msmeData = await _onboarding.GetUdyamPayload(uan);
                if (msmeData.Result != tflResultType.tflSuccess || string.IsNullOrWhiteSpace(msmeData.Data))
                {
                    model.LoadError = $"No stored Udyam response was found for {uan}.";
                    return model;
                }

                var parsed = JsonConvert.DeserializeObject<UdyamResponseModel>(msmeData.Data);
                if (parsed == null)
                {
                    model.LoadError = $"The stored Udyam response for {uan} could not be read.";
                    return model;
                }

                return UdyamController.BuildAnalysis(parsed, uan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading Udyam details for {Uan}", uan);
                model.LoadError = "Could not load the Udyam details right now. Please try again later.";
                return model;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  ITR — Income Tax Return analysis
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Reads the ITR vendor response already stored in blob storage (never
        /// re-calls the ITR API) and builds the page model from it.
        /// </summary>
        public async Task<ItrDetailsViewModel> GetItrAsync(string? uan, CancellationToken ct = default)
        {
            var model = new ItrDetailsViewModel { Uan = uan };

            if (string.IsNullOrWhiteSpace(uan))
            {
                model.LoadError = "No Udyam number is linked to this account, so there are no ITR details to display.";
                return model;
            }

            string? json;
            try
            {
                json = await _store.DownloadAsync(uan, MsmeDataFiles.Itr, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed reading ITR response from blob for {Uan}", uan);
                model.LoadError = "Could not read the Income Tax Return right now. Please try again later.";
                return model;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                model.LoadError = $"No Income Tax Return has been fetched for {uan} yet. "
                                + "Use the ITR option on the dashboard to fetch it.";
                return model;
            }

            try
            {
                var root = JObject.Parse(json);

                // The vendor reports failures in-band; a non-SUCCESS body carries no return.
                var status = root.Value<string>("status");
                if (!string.IsNullOrWhiteSpace(status)
                    && !status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    model.LoadError = root.Value<string>("message") ?? "The ITR service returned no data.";
                    return model;
                }

                return ItrController.BuildAnalysis(root, uan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed parsing ITR response for {Uan}", uan);
                model.LoadError = "The stored Income Tax Return could not be read.";
                return model;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  MCA — Corporate Intelligence Report
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Udyam organisation types that actually appear on the MCA register.
        /// Companies and LLPs are incorporated with the Registrar of Companies and
        /// get a CIN/LLPIN plus DIN-holding directors; proprietorships, partnership
        /// firms, HUFs, trusts and co-operative societies are not registered there.
        /// </summary>
        public static bool IsMcaRegistered(string? organizationType)
            => organizationType != null
               && (organizationType.Contains("Company", StringComparison.OrdinalIgnoreCase)
                   || organizationType.Contains("LLP", StringComparison.OrdinalIgnoreCase)
                   || organizationType.Contains("Limited Liability", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The registries that always apply, whatever the constitution — offered on
        /// the MCA screen when MCA itself does not. <paramref name="url"/> maps a
        /// source key to a link, because the customer pages and the bank portal's
        /// tabbed Customer 360 address the same sections differently.
        /// </summary>
        public static List<McaSourceOption> SourceOptions(Func<string, string> url) => new()
        {
            new() { Name = "Udyam Identity", Url = url("udyam"), Icon = "bi-grid-3x3-gap",
                    Description = "Registration, activity, plants and NIC classification" },
            new() { Name = "GST Analysis", Url = url("gst"), Icon = "bi-receipt",
                    Description = "Filing discipline, turnover trend and ITC behaviour" },
            new() { Name = "ITR Details", Url = url("itr"), Icon = "bi-file-earmark-text",
                    Description = "Declared income, margins and tax compliance" },
            new() { Name = "AA Details", Url = url("aa"), Icon = "bi-diagram-3",
                    Description = "Bank statement cashflow, balances and returns" },
        };

        public async Task<McaDetailsViewModel> GetMcaAsync(string? uan, CancellationToken ct = default)
        {
            var model = new McaDetailsViewModel { Uan = uan };

            if (string.IsNullOrWhiteSpace(uan))
            {
                model.LoadError = "No Udyam number is linked to this account, so there are no MCA details to display.";
                return model;
            }

            try
            {
                var json = await LoadMcaJsonAsync(uan, ct);
                var mca = string.IsNullOrWhiteSpace(json)
                    ? null
                    : JsonConvert.DeserializeObject<McaResponseModel>(json);

                if (mca?.message?.details == null)
                {
                    // Only companies and LLPs are on the MCA register — a partnership
                    // firm or proprietorship has no CIN and no DIN, so "not found" here
                    // is the correct answer, not a failure.
                    var org = (await GetUdyamAsync(uan))?.OrganizationType;
                    model.OrganizationType = string.IsNullOrWhiteSpace(org) || org == "-" ? null : org;
                    model.IsApplicable = IsMcaRegistered(org);
                    model.LoadError = model.IsApplicable
                        ? $"No stored MCA response was found for {uan}."
                        : null;   // the view renders a proper not-applicable state instead
                    return model;
                }

                var analysis = McaController.BuildAnalysis(mca, uan);
                analysis.OrganizationType = (await GetUdyamAsync(uan))?.OrganizationType;
                return analysis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading MCA details for {Uan}", uan);
                model.LoadError = "Could not load the MCA details right now. Please try again later.";
                return model;
            }
        }

        /// <summary>
        /// MCA JSON for a UAN — primary source is m_StaticResponces.MCAResponce;
        /// until that column is populated, falls back to the seeded mca.json in the
        /// UAN's blob folder.
        /// </summary>
        public async Task<string?> LoadMcaJsonAsync(string uan, CancellationToken ct = default)
        {
            var json = await _staticResponses.GetStaticCommonResponce(uan, StaticResponseType.Mca);
            if (!string.IsNullOrWhiteSpace(json))
                return json;

            return await _store.DownloadAsync(uan, MsmeDataFiles.Mca, ct);
        }

        // ─────────────────────────────────────────────────────────────
        //  GST — stored GSTR-2B / 3B returns
        // ─────────────────────────────────────────────────────────────
        public async Task<GSTAnalysisViewModel> GetGstAsync(string? uan, string? enterpriseName = null, string? pan = null)
        {
            var model = new GSTAnalysisViewModel
            {
                Uan = uan,
                EnterpriseName = enterpriseName,
                PanNumber = pan
            };

            if (string.IsNullOrWhiteSpace(uan))
            {
                model.LoadError = "No Udyam number is linked to this account, so there is no GST data to display.";
                return model;
            }

            try
            {
                var data = await _gstResponces.GetResponcesByUan(uan);
                if (data == null)
                {
                    model.LoadError = "No GST return data is stored for this MSME yet.";
                    return model;
                }

                model.HasData = true;
                model.Gstin = data.GSTINNumber;
                model.FilingPeriod = data.FilingPeriod;
                model.CreatedDate = data.CreatedDate;
                model.GSTR2BResponseData = data.GSTR2BResponseData;
                model.GSTR3BResponseData = data.GSTR3BResponseData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading GST analysis for {Uan}", uan);
                model.LoadError = "Could not load the GST analysis right now. Please try again later.";
            }

            return model;
        }

        // ─────────────────────────────────────────────────────────────
        //  Source-IP security audit
        // ─────────────────────────────────────────────────────────────
        /// <summary>
        /// IP risk profile for a UAN. <paramref name="ip"/> is the address to display
        /// — the session IP for the customer's own popup, or the IP captured at
        /// registration when a bank officer reviews the file.
        /// </summary>
        public async Task<IpVerificationViewModel> GetIpAuditAsync(string? uan, string? ip, CancellationToken ct = default)
        {
            var vm = new IpVerificationViewModel();

            try
            {
                var json = await _ipRisk.GetIpRiskScoreAsync(ip ?? string.Empty, uan ?? string.Empty, ct);
                if (!string.IsNullOrWhiteSpace(json))
                    vm = IpVerificationViewModel.FromJson(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading IP risk score for {Uan}", uan);
            }

            // Until the production Signzy key is live the stored response may be
            // empty — show the static profile so the panel never renders blank.
            if (!vm.HasData)
                vm = IpVerificationViewModel.Demo();

            if (!string.IsNullOrWhiteSpace(ip))
                vm.Ip = ip;

            return vm;
        }
    }
}
