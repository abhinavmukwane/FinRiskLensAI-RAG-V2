using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.ICommon;
using FinRiskLensAI.Core.Interfaces.IRepositories.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Core.Models.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using static FinRiskLensAI.Core.Models.AccountAggregator.ConsentReqRespModel;
using static FinRiskLensAI.Core.Models.AccountAggregator.ConsentStatusRespModel;
using static FinRiskLensAI.Core.Models.AccountAggregator.FinInfoReqRespModel;
using static FinRiskLensAI.Core.Models.AccountAggregator.FinInfoReqStatusModel;
using static FinRiskLensAI.Core.Models.AccountAggregator.FinvuTokenModel;

namespace FinRiskLensAI.Services.Implementation.AccountAggregator
{
    public class AccountAggregatorService : IAccountAggregatorService
    {
        private readonly IAccountAggregatorRepository _aaRepo;
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly FinvuSettings _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMsmeDataStore _store;
        private readonly ILogger<AccountAggregatorService> _logger;

        public AccountAggregatorService(
            IAccountAggregatorRepository aaRepo, IHttpClientHelper httpClientHelper,
            FinvuSettings config, IHttpContextAccessor httpContextAccessor,
            IMsmeDataStore store, ILogger<AccountAggregatorService> logger)
        {
            _aaRepo = aaRepo; _httpClientHelper = httpClientHelper;
            _config = config; _httpContextAccessor = httpContextAccessor;
            _store = store; _logger = logger;
        }

        // ── helpers ────────────────────────────────────────────────────

        /// <summary>
        /// AA consent-callback URL: the configured override if set, otherwise
        /// built from the current request so it's correct both locally and after publish.
        /// </summary>
        private string ResolveCallbackUrl(string trnxid)
        {
            if (!string.IsNullOrWhiteSpace(_config.CallbackUrl))
                return _config.CallbackUrl;

            var req = _httpContextAccessor.HttpContext?.Request;
            return req != null
                ? $"{req.Scheme}://{req.Host}/Home/AAConsentCallback/{trnxid}/"
                : $"/Home/AAConsentCallback/{trnxid}";
        }

        private object BuildFinvuHeader() => new
        {
            rid = Guid.NewGuid().ToString(),
            ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "+00:00",
            channelId = _config.FinvuChannelId
        };

        private static Dictionary<string, string> BuildAuthHeader(string token) =>
            new() { { "Authorization", "Bearer " + token } };

        /// <summary>Ensures a valid Finvu token and returns it, or null if login failed.</summary>
        private async Task<string> GetValidTokenAsync()
        {
            if (!_aaRepo.CheckAATokenValidity())
                await GenerateFinvuToken().ConfigureAwait(false);

            var token = _aaRepo.GetAAToken()?.token;
            if (string.IsNullOrWhiteSpace(token))
                _logger.LogError("Finvu token unavailable after login attempt.");
            return token;
        }

        // ── token ──────────────────────────────────────────────────────

        public async Task GenerateFinvuToken()
        {
            try
            {
                var payload = new
                {
                    header = BuildFinvuHeader(),
                    body = new { userId = _config.AaUserId, password = _config.AaPassword }
                };

                string responseContent = await _httpClientHelper
                    .SendPostRequest(_config.FinvuApi + "/User/Login", JsonConvert.SerializeObject(payload), null)
                    .ConfigureAwait(false);

                var resp = JsonConvert.DeserializeObject<TokenResponse>(responseContent);
                if (resp?.body?.token == null)
                {
                    _logger.LogError("Finvu login returned no token.");
                    return;
                }

                var tokenData = _aaRepo.GetAAToken() ?? new AccAggreTokenModel();
                tokenData.rid = resp.header?.rid;
                tokenData.ts = resp.header?.ts;
                tokenData.token = resp.body.token;
                tokenData.UpdatedOn = DateTime.Now;

                var updated = await _aaRepo.UpdateAAToken(tokenData).ConfigureAwait(false);
                if (updated?.Result != tflResultType.tflSuccess)
                    _logger.LogError("Failed to persist Finvu token: {Message}", updated?.Message);
                else
                    _logger.LogInformation("Finvu token refreshed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateFinvuToken failed.");
                throw;
            }
        }

        // ── consent request ────────────────────────────────────────────

        public async Task<string> CreateConsentRequest(string uan, string AAID)
        {
            if (string.IsNullOrWhiteSpace(uan) || string.IsNullOrWhiteSpace(AAID))
            {
                _logger.LogWarning("CreateConsentRequest: uan/AAID missing.");
                return null;
            }

            try
            {
                var token = await GetValidTokenAsync().ConfigureAwait(false);
                if (token == null) return null;

                string transactionId = Guid.NewGuid().ToString();
                var payload = new
                {
                    header = BuildFinvuHeader(),
                    body = new
                    {
                        custId = AAID,
                        consentDescription = "Wealth Management Service",
                        templateName = "FINVUDEMO_TESTING",
                        aaId = "cookiejar-aa@finvu.in",
                        redirectUrl = ResolveCallbackUrl(transactionId)
                    }
                };

                HttpResponseMessage response = await _httpClientHelper
                    .SendPostRequestFullResp(_config.FinvuApi + "/ConsentRequestPlus",
                        JsonConvert.SerializeObject(payload), BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("CreateConsentRequest HTTP {Status} for {Uan}: {Body}",
                        (int)response.StatusCode, uan, responseContent);
                    return null;
                }

                var resp = JsonConvert.DeserializeObject<ConsentRespModel>(responseContent);
                if (resp?.body?.url == null)
                {
                    _logger.LogError("CreateConsentRequest returned no url for {Uan}.", uan);
                    return null;
                }

                var aaConReqMdl = new AAConsentReqModel
                {
                    uan = uan,
                    custId = AAID,
                    transactionId = transactionId,
                    rid = resp.header?.rid,
                    ts = resp.header?.ts,
                    channelId = resp.header?.channelId,
                    encryptedRequest = resp.body?.encryptedRequest ?? Guid.NewGuid().ToString(),
                    requestDate = resp.body?.requestDate ?? DateTime.Now.ToString(),
                    encryptedFiuId = resp.body?.encryptedFiuId ?? Guid.NewGuid().ToString(),
                    consentHandle = resp.body.ConsentHandle,
                    url = resp.body.url,
                    CreatedOn = DateTime.Now
                };

                var saveResult = await _aaRepo.AddAAConsentRequest(aaConReqMdl).ConfigureAwait(false);
                if (saveResult.Result != tflResultType.tflSuccess)
                {
                    _logger.LogError("AddAAConsentRequest failed for {Uan}: {Message}", uan, saveResult.Message);
                    return null;
                }

                _logger.LogInformation("Consent request created for {Uan}, txn {Txn}.", uan, transactionId);
                return resp.body.url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateConsentRequest failed for {Uan}.", uan);
                return null;
            }
        }

        public async Task<AAConsentReqModel> GetAAConsentData(string transactionId)
            => await _aaRepo.GetAAConsentRequest(transactionId).ConfigureAwait(false);

        // ── consent status ─────────────────────────────────────────────

        public async Task<ConsentDetailsById> CheckConsentStatus(string trnxid, string AAID)
        {
            try
            {
                var consentReqData = await _aaRepo.GetAAConsentRequest(trnxid).ConfigureAwait(false);
                if (consentReqData == null || string.IsNullOrWhiteSpace(consentReqData.consentHandle))
                {
                    _logger.LogWarning("CheckConsentStatus: no consent record for {Trnx}.", trnxid);
                    return null;
                }

                var token = await GetValidTokenAsync().ConfigureAwait(false);
                if (token == null) return null;

                HttpResponseMessage response = await _httpClientHelper
                    .SendGetRequestFullResp(
                        _config.FinvuApi + $"/ConsentStatus/{consentReqData.consentHandle}/{AAID}",
                        BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ConsentStatus HTTP {Status} for {Trnx}: {Body}",
                        (int)response.StatusCode, trnxid, responseContent);
                    return null;
                }

                var statusResp = JsonConvert.DeserializeObject<ConsentStatusModel>(responseContent);
                _logger.LogInformation("Consent {Trnx} status: {Status}.", trnxid, statusResp?.body?.consentStatus);

                if (statusResp?.body?.consentStatus != "ACCEPTED")
                    return null;

                HttpResponseMessage detailResp = await _httpClientHelper
                    .SendGetRequestFullResp(
                        _config.FinvuApi + $"/Consent/{statusResp.body.consentId}",
                        BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string detailContent = await detailResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!detailResp.IsSuccessStatusCode)
                {
                    _logger.LogError("Consent detail HTTP {Status} for {Trnx}: {Body}",
                        (int)detailResp.StatusCode, trnxid, detailContent);
                    return null;
                }

                return JsonConvert.DeserializeObject<ConsentDetailsById>(detailContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckConsentStatus failed for {Trnx}.", trnxid);
                return null;
            }
        }

        // ── financial information (FI) pipeline ─────────────────────────

        public async Task<FinInfoRespModel> FinancialInfoRequest(
            string consentHandle, string AAID, string consentId, string dateFrom, string dateTo)
        {
            try
            {
                var token = await GetValidTokenAsync().ConfigureAwait(false);
                if (token == null) return null;

                var payload = new
                {
                    header = BuildFinvuHeader(),
                    body = new
                    {
                        custId = AAID,
                        consentHandleId = consentHandle,
                        consentId = consentId,
                        dateTimeRangeFrom = dateFrom,
                        dateTimeRangeTo = dateTo
                    }
                };

                HttpResponseMessage response = await _httpClientHelper
                    .SendPostRequestFullResp(_config.FinvuApi + "/FIRequest",
                        JsonConvert.SerializeObject(payload), BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FIRequest HTTP {Status}: {Body}", (int)response.StatusCode, responseContent);
                    return null;
                }

                return JsonConvert.DeserializeObject<FinInfoRespModel>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialInfoRequest failed.");
                return null;
            }
        }

        public async Task<FinInfoRespStatusModel> FinancialInfoReqStatus(
            string consentHandle, string AAID, string consentId, string sessionId)
        {
            try
            {
                var token = await GetValidTokenAsync().ConfigureAwait(false);
                if (token == null) return null;

                HttpResponseMessage response = await _httpClientHelper
                    .SendGetRequestFullResp(
                        _config.FinvuApi + $"/FIStatus/{consentId}/{sessionId}/{consentHandle}/{AAID}",
                        BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FIStatus HTTP {Status}: {Body}", (int)response.StatusCode, responseContent);
                    return null;
                }

                return JsonConvert.DeserializeObject<FinInfoRespStatusModel>(responseContent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialInfoReqStatus failed.");
                return null;
            }
        }

        public async Task<string> FinancialInfoFetch(string AAID, string consentId, string sessionId)
        {
            try
            {
                var token = await GetValidTokenAsync().ConfigureAwait(false);
                if (token == null) return null;

                HttpResponseMessage response = await _httpClientHelper
                    .SendGetRequestFullResp(
                        _config.FinvuApi + $"/FIFetch/{AAID}/{consentId}/{sessionId}",
                        BuildAuthHeader(token))
                    .ConfigureAwait(false);

                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("FIFetch HTTP {Status}: {Body}", (int)response.StatusCode, responseContent);
                    return null;
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinancialInfoFetch failed.");
                return null;
            }
        }

        // ── orchestration: ACTIVE consent → FI request → status → fetch → blob ──

        public async Task<bool> FetchAndStoreFinancialData(string trnxid, ConsentDetailsById consentDetails)
        {
            if (string.IsNullOrWhiteSpace(trnxid) || consentDetails?.Body == null)
            {
                _logger.LogWarning("FetchAndStoreFinancialData: missing trnxid or consent details.");
                return false;
            }

            var consent = await _aaRepo.GetAAConsentRequest(trnxid).ConfigureAwait(false);
            if (consent == null || string.IsNullOrWhiteSpace(consent.consentHandle)
                || string.IsNullOrWhiteSpace(consent.custId) || string.IsNullOrWhiteSpace(consent.uan))
            {
                _logger.LogWarning("FetchAndStoreFinancialData: consent record incomplete for {Trnx}.", trnxid);
                return false;
            }

            var consentId = consentDetails.Body.ConsentId;
            var range = consentDetails.Body.ConsentDetail?.FIDataRange;
            if (string.IsNullOrWhiteSpace(consentId) || range == null)
            {
                _logger.LogWarning("FetchAndStoreFinancialData: consentId/FIDataRange missing for {Trnx}.", trnxid);
                return false;
            }

            string dateFrom = range.From.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "+0000";
            string dateTo = range.To.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture) + "+0000";

            // 1) FI Request
            var fiReq = await FinancialInfoRequest(consent.consentHandle, consent.custId, consentId, dateFrom, dateTo).ConfigureAwait(false);
            if (fiReq?.body == null || fiReq.body.errorCode != 0 || string.IsNullOrWhiteSpace(fiReq.body.sessionId))
            {
                _logger.LogError("FI request failed for {Uan}: code={Code} msg={Msg}",
                    consent.uan, fiReq?.body?.errorCode, fiReq?.body?.errorMsg);
                return false;
            }
            var sessionId = fiReq.body.sessionId;

            await Task.Delay(3000); // Wait for 3 seconds

            // 2) FI Status
            var fiStatus = await FinancialInfoReqStatus(consent.consentHandle, consent.custId, consentId, sessionId).ConfigureAwait(false);
            if (fiStatus?.body == null || fiStatus.body.errorCode != 0)
            {
                _logger.LogError("FI status failed for {Uan}: code={Code} msg={Msg}",
                    consent.uan, fiStatus?.body?.errorCode, fiStatus?.body?.errorMsg);
                return false;
            }
            _logger.LogInformation("FI status for {Uan}: {Status}.", consent.uan, fiStatus.body.fiRequestStatus);

            await Task.Delay(3000); // Wait for 3 seconds

            // 3) FI Fetch
            var data = await FinancialInfoFetch(consent.custId, consentId, sessionId).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(data))
            {
                _logger.LogError("FI fetch returned no data for {Uan}.", consent.uan);
                return false;
            }

            // 4) Store to blob (overwrite updates an existing aa.json)
            await _store.UploadAsync(consent.uan, MsmeDataFiles.Aa, data).ConfigureAwait(false);
            _logger.LogInformation("Stored AA data to {Uan}/{File}.", consent.uan, MsmeDataFiles.Aa);
            return true;
        }
    }
}
