using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator;
using FinRiskLensAI.Core.Interfaces.IServices.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.Knowledge;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.Fraud.Ai;
using Microsoft.Extensions.Logging;

namespace FinRiskLensAI.Fraud.Credit
{
    /// <summary>A decision as Gemini proposed it — untrusted until <see cref="CreditDecisionService.Ground"/>.</summary>
    public sealed class ProposedDecision
    {
        public string? Outcome { get; set; }
        [JsonPropertyName("recommended_amount")] public decimal RecommendedAmount { get; set; }
        public List<ProposedReason>? Reasons { get; set; }
        public List<string>? Conditions { get; set; }
    }

    public sealed class ProposedReason
    {
        public string? Text { get; set; }
        public List<long>? Citations { get; set; }
    }

    public sealed record GroundedDecision(CreditOutcome Outcome, decimal RecommendedAmount, List<DecisionReason> Reasons,
                                          List<string> Conditions, List<DroppedReason> Dropped);

    /// <summary>
    /// T9 (Doc/13_TEAM_WORK_PLAN.md): Gemini Pro decides a Clear customer's loan within the credit capacity computed
    /// in code (<see cref="CreditCapacityCalculator"/>), citing the credit policy. Then the same rule as the
    /// Investigator: every rupee figure must be one of the capacity's, every citation a chunk the search returned,
    /// and the amount never above the maximum — what breaks a rule is removed and stored as dropped.
    /// </summary>
    public class CreditDecisionService : ICreditDecisionService
    {
        private const string Instruction = """
            You are a credit officer at an Indian lender, deciding one MSME loan. The fraud and risk checks found nothing
            open on this customer. The credit capacity below was computed in code from the customer's Account Aggregator
            bank data and ITR under the credit policy — use it as given, never recompute it.

            Decide:
            - outcome: APPROVE when the requested loan fits the capacity and the policy; REFER when the policy sends it
              to a higher approver or something must happen first; DECLINE when the customer cannot carry a new EMI.
            - recommended_amount: at most max_loan_amount, in whole rupees; 0 when declining. When the request is above
              the maximum, recommend the maximum or less.
            - reasons: 2 to 5 short points an underwriter can check, each citing the chunk_id of the policy extract it applies.
            - conditions: what must hold before disbursal, if anything.

            Rules — a reason that breaks one is removed before anyone sees it:
            - Rupee amounts only from the capacity or your recommended amount, in full rupees with Indian grouping,
              e.g. ₹1,23,456. Never estimate, round, annualise, or write lakh or crore. Percentages are fine.
            - Cite only chunk_ids from the policy extracts below.
            """;

        // What the decision leans on in the policy (Doc/knowledge/credit-policy.md §3, §5.3, §7)
        private static readonly string[] PolicyQueries =
        {
            "FOIR limits by assessed monthly income",
            "assessed income is the lower of bank and ITR income",
            "EMIs on the bank statement count towards FOIR",
            "escalation matrix approver above FOIR limit",
            "tax audit report 44AB loan amount cap"
        };

        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private readonly ICaseRepository _cases;
        private readonly GeminiClient _gemini;
        private readonly IKnowledgeService _knowledge;
        private readonly IBlobAnalysisService _facts;
        private readonly IMsmeDataStore _sources;
        private readonly IAaStatementAnalysisService _aa;
        private readonly ILogger<CreditDecisionService> _log;

        public CreditDecisionService(ICaseRepository cases, GeminiClient gemini, IKnowledgeService knowledge, IBlobAnalysisService facts,
                                     IMsmeDataStore sources, IAaStatementAnalysisService aa, ILogger<CreditDecisionService> log)
        {
            _cases = cases;
            _gemini = gemini;
            _knowledge = knowledge;
            _facts = facts;
            _sources = sources;
            _aa = aa;
            _log = log;
        }

        public async Task<(CreditDecision? Decision, string? Error)> DecideAsync(string uan, CreditRequest request, string actor, CancellationToken ct = default)
        {
            if (request.RequestedAmount <= 0 || request.TenureMonths is < 1 or > 360 || request.RatePct is <= 0 or > 50)
                return (null, "Enter an amount above zero, a tenure of 1–360 months and a rate between 0 and 50%.");
            if (!_gemini.Options.IsConfigured) return (null, "Gemini is not configured (Gcp:ProjectId).");

            var (app, verdict) = await VerdictAsync(uan, ct);
            if (app == null) return (null, "This customer has no assessment yet.");
            if (verdict != RiskVerdict.Clear) return (null, $"A loan case needs a Clear verdict — this customer is {RiskSummary.Label(verdict)}.");

            var features = (await _facts.GetFactsAsync(uan, ct))?.Features ?? new MsmeFeatureSet();
            var aaJson = await _sources.DownloadAsync(uan, MsmeDataFiles.Aa, ct);
            var bank = aaJson == null ? null : _aa.Analyze(aaJson);

            CreditCapacity capacity;
            try
            {
                capacity = CreditCapacityCalculator.Compute(features, bank, request.RequestedAmount, request.TenureMonths, request.RatePct);
            }
            catch (NotImplementedException)
            {
                return (null, "The credit capacity calculator (T6) is not built yet — the decision needs its figures.");
            }

            var chunks = new Dictionary<long, KnowledgeHit>();
            foreach (var q in PolicyQueries)   // one at a time: the search shares this request's DbContext
                foreach (var h in await _knowledge.SearchAsync(q, new[] { KnowledgeCorpus.Policy }, topK: 3, ct))
                    chunks.TryAdd(h.ChunkId, h);

            var prompt = new JsonObject
            {
                ["customer"] = app.ApplicantRef,
                ["product"] = request.Product == LoanProduct.WorkingCapital ? "Working capital" : "Term loan",
                ["credit_capacity"] = JsonSerializer.SerializeToNode(capacity, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }),
                ["policy_extracts"] = new JsonArray(chunks.Values.Select(h => (JsonNode)new JsonObject
                {
                    ["chunk_id"] = h.ChunkId, ["source"] = h.Breadcrumb ?? h.Title, ["text"] = h.Text
                }).ToArray())
            };

            var o = _gemini.Options;
            ProposedDecision proposed;
            string model = o.ProModel;
            try
            {
                proposed = await AskAsync(model, "LOW", prompt, ct);
            }
            catch (GeminiException ex) when (o.ProFallbackModel != o.ProModel)
            {
                _log.LogWarning("Credit decision on {Model} failed ({Error}); retrying on {Fallback}", o.ProModel, ex.Message, o.ProFallbackModel);
                model = o.ProFallbackModel;
                proposed = await AskAsync(model, null, prompt, ct);   // thinkingLevel is a Gemini 3 setting
            }

            var g = Ground(proposed, capacity, chunks.ToDictionary(c => c.Key, c => c.Value.Breadcrumb ?? c.Value.Title));
            var decision = new CreditDecision
            {
                LoanApplicationID = app.LoanApplicationID,
                Uan = uan,
                Product = request.Product,
                RequestedAmount = request.RequestedAmount,
                TenureMonths = request.TenureMonths,
                RatePct = request.RatePct,
                Capacity = JsonSerializer.Serialize(capacity, Json),
                Outcome = g.Outcome,
                RecommendedAmount = g.RecommendedAmount,
                Reasons = JsonSerializer.Serialize(g.Reasons, Json),
                Conditions = JsonSerializer.Serialize(g.Conditions, Json),
                Dropped = JsonSerializer.Serialize(g.Dropped, Json),
                Model = model,
                CreatedBy = actor,
                UpdatedBy = actor
            };
            await _cases.AddCreditDecisionAsync(decision, ct);
            _log.LogInformation("Credit decision {Id} for {Uan}: {Outcome} ₹{Amount} on {Model}, {Dropped} dropped",
                                decision.CreditDecisionID, uan, g.Outcome, g.RecommendedAmount, model, g.Dropped.Count);
            return (decision, null);
        }

        public async Task<string?> AcceptAsync(string uan, int creditDecisionId, decimal amount, string actor, CancellationToken ct = default)
        {
            var (app, verdict) = await VerdictAsync(uan, ct);
            if (app == null) return "This customer has no assessment yet.";
            if (verdict != RiskVerdict.Clear) return $"The customer is no longer Clear ({RiskSummary.Label(verdict)}) — reassess before accepting.";

            var latest = await _cases.GetLatestCreditDecisionAsync(app.LoanApplicationID, ct);
            if (latest == null || latest.CreditDecisionID != creditDecisionId) return "A newer decision exists — accept that one.";
            if (latest.Outcome != CreditOutcome.Approve) return $"Only an Approve can be accepted; this decision is {latest.Outcome}.";
            if (amount <= 0 || amount > latest.RecommendedAmount)
                return $"The accepted amount must be above zero and at most the recommended ₹{latest.RecommendedAmount.ToString("N0", Inr)}.";

            return await _cases.AcceptCreditDecisionAsync(creditDecisionId, app.LoanApplicationID, decimal.Round(amount), actor, ct)
                ? null : "The decision could not be updated.";
        }

        public async Task<CreditDecision?> GetLatestAsync(string uan, CancellationToken ct = default)
            => await _cases.FindByUanAsync(uan, ct) is int id ? await _cases.GetLatestCreditDecisionAsync(id, ct) : null;

        public async Task<string?> PushBlockedAsync(string uan, CancellationToken ct = default)
        {
            var (app, verdict) = await VerdictAsync(uan, ct);
            if (app == null) return "This customer has no risk assessment yet.";
            if (verdict != RiskVerdict.Clear) return $"The Risk Lens verdict is {RiskSummary.Label(verdict)} — only a Clear customer's loan case can be sent.";
            var latest = await _cases.GetLatestCreditDecisionAsync(app.LoanApplicationID, ct);
            if (latest == null) return "Generate the credit decision first.";
            if (latest.Outcome != CreditOutcome.Approve) return $"The latest credit decision is {latest.Outcome} — only an approved loan can be sent.";
            if (latest.AcceptedAt == null) return "Accept the credit decision first.";
            return null;
        }

        /// <summary>
        /// The verifier: outcome and amount within the capacity, each reason's rupee figures one of the capacity's
        /// (or the recommended amount), citations only to retrieved policy. A decision with no reason left is referred.
        /// </summary>
        public static GroundedDecision Ground(ProposedDecision p, CreditCapacity capacity, IReadOnlyDictionary<long, string> chunks)
        {
            var dropped = new List<DroppedReason>();
            var max = Math.Max(0, decimal.Round(capacity.MaxLoanAmount));

            var outcome = p.Outcome?.Trim().ToUpperInvariant() switch
            {
                "APPROVE" => CreditOutcome.Approve,
                "DECLINE" => CreditOutcome.Decline,
                "REFER" => CreditOutcome.Refer,
                var other => Drop(CreditOutcome.Refer, $"outcome '{other}'", "not Approve, Refer or Decline — referred")
            };
            var amount = outcome == CreditOutcome.Decline ? 0 : Math.Max(0, decimal.Round(p.RecommendedAmount));
            if (amount > max)
            {
                dropped.Add(new($"Recommended ₹{amount.ToString("N0", Inr)}", $"above the maximum loan ₹{max.ToString("N0", Inr)} — capped"));
                amount = max;
            }
            if (outcome == CreditOutcome.Approve && amount == 0)
                outcome = Drop(CreditOutcome.Refer, "Approve with no amount", "nothing to lend within the capacity — referred");

            var allowed = capacity.Figures.Append(amount).ToList();
            string? Untraced(string text) => GroundingVerifier.Amounts(text).Where(a => !allowed.Any(x => Math.Abs(x - a) <= 1m))
                                                                           .Select(a => $"₹{a.ToString("N0", Inr)}").FirstOrDefault();

            var reasons = new List<DecisionReason>();
            foreach (var r in p.Reasons ?? new())
            {
                if (string.IsNullOrWhiteSpace(r.Text)) continue;
                if (Untraced(r.Text) is string bad) { dropped.Add(new(r.Text.Trim(), $"quotes {bad}, which is not a credit-capacity figure")); continue; }
                // Citations the search never returned are stripped; the reason stands on the rest
                var sources = (r.Citations ?? new()).Distinct().Where(chunks.ContainsKey).Select(c => chunks[c]).ToList();
                reasons.Add(new(r.Text.Trim(), sources));
            }

            var conditions = new List<string>();
            foreach (var c in (p.Conditions ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)))
                if (Untraced(c) is string bad) dropped.Add(new(c.Trim(), $"quotes {bad}, which is not a credit-capacity figure"));
                else conditions.Add(c.Trim());

            if (reasons.Count == 0 && outcome != CreditOutcome.Refer)
                outcome = Drop(CreditOutcome.Refer, $"{outcome}", "no reason survived the grounding check — referred");

            return new GroundedDecision(outcome, outcome == CreditOutcome.Decline ? 0 : amount, reasons, conditions, dropped);

            CreditOutcome Drop(CreditOutcome to, string text, string why) { dropped.Add(new(text, why)); return to; }
        }

        private async Task<(LoanApplication? App, RiskVerdict Verdict)> VerdictAsync(string uan, CancellationToken ct)
        {
            if (await _cases.FindByUanAsync(uan, ct) is not int id || await _cases.GetAsync(id, ct) is not { } app)
                return (null, RiskVerdict.AwaitingData);
            return (app, RiskSummary.Of(app, await _cases.GetFindingsAsync(id, ct)));
        }

        private Task<ProposedDecision> AskAsync(string model, string? thinking, JsonObject prompt, CancellationToken ct)
            => _gemini.GenerateJsonAsync<ProposedDecision>(model, _gemini.Options.ProLocation, Instruction,
                   new JsonNode[] { GeminiClient.Text(prompt.ToJsonString()) }, Schema(), maxOutputTokens: 8192, thinking, ct);

        private static JsonObject Schema()
        {
            static JsonObject T(string type) => new() { ["type"] = type };
            return new JsonObject
            {
                ["type"] = "OBJECT",
                ["properties"] = new JsonObject
                {
                    ["outcome"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("APPROVE", "REFER", "DECLINE") },
                    ["recommended_amount"] = T("NUMBER"),
                    ["reasons"] = new JsonObject
                    {
                        ["type"] = "ARRAY",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "OBJECT",
                            ["properties"] = new JsonObject { ["text"] = T("STRING"), ["citations"] = new JsonObject { ["type"] = "ARRAY", ["items"] = T("INTEGER") } },
                            ["required"] = new JsonArray("text", "citations")
                        }
                    },
                    ["conditions"] = new JsonObject { ["type"] = "ARRAY", ["items"] = T("STRING") }
                },
                ["required"] = new JsonArray("outcome", "recommended_amount", "reasons", "conditions")
            };
        }

        private static readonly CultureInfo Inr = CultureInfo.GetCultureInfo("en-IN");
    }
}
