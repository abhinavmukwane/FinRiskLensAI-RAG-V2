using System.Security.Cryptography;
using System.Text;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.ICommon;
using FinRiskLensAI.Core.Interfaces.IRepositories.Admin;
using FinRiskLensAI.Core.Interfaces.IServices.Admin;
using FinRiskLensAI.Core.Interfaces.IServices.LoanCase;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Core.Models.Scoring;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Services.Implementation.LoanCase
{
    /// <summary>
    /// Raises a scored MSME as a loan case downstream. One service, one table, three
    /// payload builders. Preview and push run the same builder over the same inputs,
    /// so the bytes an officer previews are the bytes that get sent.
    /// </summary>
    public class LoanCaseService : ILoanCaseService
    {
        private static readonly JsonSerializerSettings Pretty = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DateFormatString = "yyyy-MM-dd'T'HH:mm:ss'Z'",
            DateTimeZoneHandling = DateTimeZoneHandling.Utc
        };

        private readonly IReadOnlyDictionary<LoanCaseChannel, ILoanCasePayloadBuilder> _builders;
        private readonly ILoanCasePushRepository _pushes;
        private readonly IBankAdminService _bankAdmin;
        private readonly IBlobAnalysisService _analysis;
        private readonly IHttpClientHelper _http;
        private readonly LoanCaseSettings _settings;
        private readonly ILogger<LoanCaseService> _logger;

        public LoanCaseService(IEnumerable<ILoanCasePayloadBuilder> builders, ILoanCasePushRepository pushes,
            IBankAdminService bankAdmin, IBlobAnalysisService analysis, IHttpClientHelper http,
            LoanCaseSettings settings, ILogger<LoanCaseService> logger)
        {
            _builders = builders.ToDictionary(b => b.Channel);
            _pushes = pushes;
            _bankAdmin = bankAdmin;
            _analysis = analysis;
            _http = http;
            _settings = settings;
            _logger = logger;
        }

        public bool IsLive(LoanCaseChannel channel) => _settings.EndpointFor(channel) != null;

        public async Task<LoanCasePreview> PreviewAsync(string uan, LoanCaseChannel channel, BankUserSessionModel user,
                                                        CancellationToken ct = default)
        {
            var preview = new LoanCasePreview { Uan = uan?.Trim() ?? "", Channel = channel, IsLive = IsLive(channel),
                                                Endpoint = _settings.EndpointFor(channel) };

            var (customer, result, error) = await LoadAsync(preview.Uan, ct);
            if (error != null) { preview.Error = error; return preview; }

            var ctx = await BuildContextAsync(preview.Uan, channel, user, null, ct);
            // Preview references are clearly not real case ids — a preview must never
            // be mistaken for a raised case if someone pastes it somewhere.
            ctx.CaseReference = $"PREVIEW-{channel}-{preview.Uan[^7..]}";

            preview.EnterpriseName = customer!.EnterpriseName;
            preview.Score = result!.OverallScore;
            preview.Band = result.ScoreBand.ToString();
            preview.Eligibility = (decimal)(result.Lending?.TotalIndicativeEligibility ?? 0);
            preview.PayloadJson = Serialize(_builders[channel].Build(customer, result, ctx));
            return preview;
        }

        public async Task<LoanCasePushResult> PushAsync(string uan, LoanCaseChannel channel, BankUserSessionModel user,
                                                        string? clientIp, CancellationToken ct = default)
        {
            var key = uan?.Trim() ?? "";
            var (customer, result, error) = await LoadAsync(key, ct);
            if (error != null)
                return new LoanCasePushResult { Succeeded = false, Channel = channel, Message = error };

            var ctx = await BuildContextAsync(key, channel, user, clientIp, ct);
            var payload = Serialize(_builders[channel].Build(customer!, result!, ctx));
            var endpoint = _settings.EndpointFor(channel);

            var push = new LoanCasePush
            {
                Uan = key,
                EnterpriseName = customer!.EnterpriseName,
                Channel = channel,
                Endpoint = endpoint,
                Payload = payload,
                ScoreAtPush = result!.OverallScore,
                BandAtPush = result.ScoreBand.ToString(),
                EligibilityAtPush = (decimal)(result.Lending?.TotalIndicativeEligibility ?? 0),
                ModelVersion = result.ModelVersion,
                ScoreComputedAt = result.ComputedAt,
                PushedByLoginId = user.AdmBankLoginID,
                PushedByUserId = user.UserId,
                PushedByName = user.FullName,
                PushedFromIp = clientIp,
                PushedAt = ctx.Timestamp,
                CreatedBy = user.UserId,
                UpdatedBy = user.UserId
            };

            if (endpoint == null)
            {
                // No endpoint for this channel: the payload is built and recorded
                // exactly as it would be sent, and the row says so. Pointing the
                // channel at a real URL in appsettings is the entire integration step.
                push.Status = LoanCasePushStatus.Simulated;
                push.CaseReference = ctx.CaseReference;
                push.ResponseBody = JsonConvert.SerializeObject(new
                {
                    simulated = true,
                    caseReference = ctx.CaseReference,
                    note = $"No {channel} endpoint configured (LoanCase:Endpoints:{channel}); payload recorded locally."
                }, Pretty);
                _logger.LogInformation("LoanCase {Channel} SIMULATED for {Uan} by {User}: {Ref}",
                    channel, key, user.UserId, ctx.CaseReference);
            }
            else
            {
                try
                {
                    var headers = new Dictionary<string, string> { ["Accept"] = "application/json" };
                    var apiKey = _settings.ApiKeyFor(channel);
                    if (apiKey != null) headers["Authorization"] = apiKey.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? apiKey : $"Bearer {apiKey}";

                    var response = await _http.SendPostRequestFullResp(endpoint, payload, headers);
                    var body = response.Content == null ? "" : await response.Content.ReadAsStringAsync(ct);

                    push.HttpStatus = (int)response.StatusCode;
                    push.ResponseBody = body;

                    if (response.IsSuccessStatusCode)
                    {
                        push.Status = LoanCasePushStatus.Sent;
                        // Use the downstream id when it sends one back; otherwise our own.
                        push.CaseReference = ExtractReference(body) ?? ctx.CaseReference;
                        _logger.LogInformation("LoanCase {Channel} SENT for {Uan} by {User}: {Ref} ({Status})",
                            channel, key, user.UserId, push.CaseReference, push.HttpStatus);
                    }
                    else
                    {
                        push.Status = LoanCasePushStatus.Failed;
                        push.CaseReference = ctx.CaseReference;
                        push.ErrorMessage = $"HTTP {push.HttpStatus}";
                        _logger.LogWarning("LoanCase {Channel} FAILED for {Uan}: HTTP {Status} {Body}",
                            channel, key, push.HttpStatus, body.Length > 300 ? body[..300] : body);
                    }
                }
                catch (Exception ex)
                {
                    push.Status = LoanCasePushStatus.Failed;
                    push.CaseReference = ctx.CaseReference;
                    push.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                    _logger.LogError(ex, "LoanCase {Channel} push failed for {Uan}", channel, key);
                }
            }

            var id = await _pushes.AddAsync(push, ct);

            return new LoanCasePushResult
            {
                Succeeded = push.Status != LoanCasePushStatus.Failed,
                LoanCasePushID = id,
                Channel = channel,
                Status = push.Status,
                CaseReference = push.CaseReference,
                Message = push.Status switch
                {
                    LoanCasePushStatus.Sent => $"Case {push.CaseReference} raised in {channel}.",
                    LoanCasePushStatus.Simulated => $"Case {push.CaseReference} recorded for {channel} (simulated — no endpoint configured).",
                    _ => $"{channel} rejected the case: {push.ErrorMessage}"
                }
            };
        }

        public Task<LoanCasePush?> GetPushAsync(int loanCasePushId, CancellationToken ct = default)
            => _pushes.GetAsync(loanCasePushId, ct);

        public Task<IReadOnlyList<LoanCasePushSummary>> GetStatusAsync(string uan, CancellationToken ct = default)
            => _pushes.GetLatestByChannelAsync(uan, ct);

        // ── helpers ──────────────────────────────────────────────────────────

        private async Task<(BankCustomerRow? customer, RiskAnalysisResult? result, string? error)> LoadAsync(
            string uan, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(uan)) return (null, null, "No Udyam number supplied.");

            var customer = await _bankAdmin.GetCustomerAsync(uan, ct);
            if (customer == null) return (null, null, $"No onboarded MSME found for {uan}.");

            var result = await _analysis.GetResultAsync(uan, ct);
            if (result == null) return (customer, null, "This MSME has not been scored yet — run the analysis before raising a case.");

            return (customer, result, null);
        }

        private async Task<LoanCaseContext> BuildContextAsync(string uan, LoanCaseChannel channel,
            BankUserSessionModel user, string? clientIp, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var sources = new List<string>();
            try
            {
                var status = await _analysis.GetStatusAsync(uan, ct);
                // Collapse the 84 monthly GST files into one line per return type; the
                // downstream system needs to know GST was present, not every month.
                sources = status.FilesPresent
                    .Select(f => System.Text.RegularExpressions.Regex.Replace(f, @"_\d{6}\.json$", "").Replace(".json", ""))
                    .Where(f => !f.StartsWith("_") && f != "result")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "LoanCase: could not list data sources for {Uan}", uan);
            }

            return new LoanCaseContext
            {
                CaseReference = NewCaseReference(channel, uan, now),
                TransactionId = Guid.NewGuid().ToString(),
                Timestamp = now,
                LenderId = _settings.LenderId,
                LenderName = _settings.LenderName,
                OndcSubscriberId = _settings.OndcSubscriberId,
                OndcSubscriberUri = _settings.OndcSubscriberUri,
                RequestedByUserId = user.UserId,
                RequestedByName = user.FullName,
                BranchIfsc = user.IfscCode,
                BranchName = user.BranchName,
                DataSources = sources
            };
        }

        /// <summary>e.g. IBKL-LOS-20260915-4F3A9C — lender, channel, date, short hash.</summary>
        private string NewCaseReference(LoanCaseChannel channel, string uan, DateTime at)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{uan}|{channel}|{at:O}|{Guid.NewGuid()}"));
            return $"{_settings.LenderId}-{channel}-{at:yyyyMMdd}-{Convert.ToHexString(hash)[..6]}";
        }

        private static string Serialize(object payload) => JsonConvert.SerializeObject(payload, Pretty);

        /// <summary>Best-effort pull of a case id out of whatever the downstream system replied.</summary>
        private static string? ExtractReference(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                var j = JToken.Parse(body);
                foreach (var key in new[] { "caseReference", "caseId", "applicationId", "referenceNo", "reference", "id", "order.id", "message.order.id" })
                {
                    var v = j.SelectToken(key)?.ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v.Length > 80 ? v[..80] : v;
                }
            }
            catch { /* not JSON — fall through */ }
            return null;
        }
    }
}
