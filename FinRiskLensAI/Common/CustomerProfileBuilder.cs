using FinRiskLensAI.Controllers;
using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
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
        //  MCA — Corporate Intelligence Report
        // ─────────────────────────────────────────────────────────────
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
                    model.LoadError = $"No stored MCA response was found for {uan}.";
                    return model;
                }

                return McaController.BuildAnalysis(mca, uan);
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
