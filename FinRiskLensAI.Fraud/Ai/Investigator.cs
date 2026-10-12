using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Interfaces.IServices.Knowledge;
using FinRiskLensAI.Core.Models.Cases;
using Microsoft.Extensions.Logging;

namespace FinRiskLensAI.Fraud.Ai
{
    public sealed record InvestigationResult(string Model, int Turns, int ToolCalls, int Kept, int Dropped,
                                             int CitationsStripped, string? Summary, TimeSpan Took);

    /// <summary>
    /// Step ⑤ (Doc/10_FRAUD_LENS_BUILD.md, Phase 4): Gemini Pro investigates one application
    /// through function calls, then every proposed finding passes the
    /// <see cref="GroundingVerifier"/> before anyone sees it. What survives is stored as
    /// AI+VERIFIED; what does not is logged with the reason.
    /// </summary>
    public class Investigator
    {
        private const int MaxTurns = 8;

        private const string Instruction = """
            You are a fraud investigator at an Indian lender, reviewing one loan application with tools
            that can only see this application.

            Deterministic checks have already verified the documents' arithmetic and metadata and some
            behaviour (balance continuity, page totals, salary-slip maths, round-tripping, window
            dressing, pass-through). Their results are in get_case_overview. Do not repeat them.

            Look for what arithmetic cannot see:
            - UNDECLARED_EMI: recurring fixed debits to lenders or NBFCs (NACH, ECS, EMI) beyond the declared EMIs.
            - LOAN_STACKING: two or more loan disbursals from lenders within 60 days.
            - GAMBLING_CRYPTO_EXPOSURE: payments to betting, fantasy-gaming or crypto platforms.
            - BOUNCES: returned cheques or ECS/NACH, return or bounce charges.
            - SALARY_SOURCE_MISMATCH: salary credited by someone other than the employer on the slip or application.
            - INCOME_MISMATCH: income on slips, statements, the application and the verification facts that
              do not agree. Compare turnover with ITR business_turnover, never with gross_total_income.
            - IDENTITY_MISMATCH: name, PAN, date of birth or address that differ between documents.
            - IMPLAUSIBLE_EMPLOYER: employer details that do not hold together.

            Rules — a finding that breaks one is discarded before anyone sees it:
            - Cite the exact row_id or field_id values that show the problem. Only ids a tool returned.
            - Rupee amounts in full rupees with Indian grouping, e.g. ₹1,23,456. Only quote a cited row's
              amount, the total of the cited rows, a declared figure or a verification fact. Never
              estimate, annualise, average or round, and never write lakh or crore.
            - Search the policy (search_policy) for the rule that applies, put its chunk_id in citations,
              and name the clause in recommended_action, e.g. "Sample Credit Policy §3.1".
            - Normal business or household spending is not a finding. When nothing is wrong, submit an
              empty list — that is a valid and useful result.
            - Finish by calling submit_findings exactly once.
            """;

        private readonly GeminiClient _gemini;
        private readonly ICaseRepository _cases;
        private readonly IKnowledgeService _knowledge;
        private readonly IBlobAnalysisService? _facts;
        private readonly ILogger<Investigator> _log;

        public Investigator(GeminiClient gemini, ICaseRepository cases, IKnowledgeService knowledge,
                            ILogger<Investigator> log, IBlobAnalysisService? facts = null)
        {
            _gemini = gemini;
            _cases = cases;
            _knowledge = knowledge;
            _log = log;
            _facts = facts;
        }

        public async Task<InvestigationResult> InvestigateAsync(int loanApplicationId, CancellationToken ct = default)
        {
            var o = _gemini.Options;
            try
            {
                return await RunAsync(loanApplicationId, o.ProModel, ct);
            }
            catch (GeminiException ex) when (o.ProFallbackModel != o.ProModel)
            {
                // Preview models come and go and have tighter quotas — fall back rather than lose the run
                _log.LogWarning("Investigator on {Model} failed ({Error}); retrying on {Fallback}", o.ProModel, ex.Message, o.ProFallbackModel);
                return await RunAsync(loanApplicationId, o.ProFallbackModel, ct);
            }
        }

        private async Task<InvestigationResult> RunAsync(int id, string model, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            var app = await _cases.GetAsync(id, ct) ?? throw new InvalidOperationException($"Case {id} not found.");
            var (rows, _, fields) = await _cases.GetExtractedAsync(id, ct);
            var deterministic = (await _cases.GetFindingsAsync(id, ct)).Where(f => f.DetectedBy == FindingSource.Deterministic).ToList();
            var tools = new InvestigatorTools(app, rows, fields, deterministic, _facts, _knowledge);

            var contents = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(GeminiClient.Text($"Investigate application {app.ApplicantRef}. Start with get_case_overview."))
            });

            List<ProposedFinding>? proposed = null;
            string? summary = null;
            int turn = 0;
            for (; turn < MaxTurns && proposed == null; turn++)
            {
                var last = turn == MaxTurns - 1;
                var body = new JsonObject
                {
                    ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(GeminiClient.Text(Instruction)) },
                    ["contents"] = contents.DeepClone(),
                    ["tools"] = new JsonArray(new JsonObject { ["functionDeclarations"] = InvestigatorTools.Declarations() }),
                    ["generationConfig"] = new JsonObject { ["temperature"] = 0, ["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = "LOW" } }
                };
                // Out of turns: the only call left is to submit
                if (last)
                    body["toolConfig"] = new JsonObject
                    {
                        ["functionCallingConfig"] = new JsonObject { ["mode"] = "ANY", ["allowedFunctionNames"] = new JsonArray("submit_findings") }
                    };

                var response = await _gemini.GenerateRawAsync(model, _gemini.Options.ProLocation, body, ct);
                // Echo the model's turn back verbatim — Gemini 3 thought signatures must survive the round trip
                var content = JsonNode.Parse(response.GetProperty("candidates")[0].GetProperty("content").GetRawText())!.AsObject();
                contents.Add(content);

                var calls = (content["parts"] as JsonArray ?? new()).OfType<JsonObject>()
                    .Where(p => p["functionCall"] != null).Select(p => p["functionCall"]!.AsObject()).ToList();
                if (calls.Count == 0)
                {
                    contents.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JsonArray(GeminiClient.Text("Finish by calling submit_findings."))
                    });
                    continue;
                }

                var replies = new JsonArray();
                foreach (var call in calls)
                {
                    var name = call["name"]!.GetValue<string>();
                    var args = call["args"] as JsonObject ?? new JsonObject();
                    if (name == "submit_findings")
                    {
                        summary = args["summary"]?.GetValue<string>();
                        proposed = (args["findings"] as JsonArray ?? new()).Select(f =>
                            JsonSerializer.Deserialize<ProposedFinding>(f!.ToJsonString(), Json)!).ToList();
                        break;
                    }
                    replies.Add(new JsonObject
                    {
                        ["functionResponse"] = new JsonObject
                        {
                            ["name"] = name,
                            ["response"] = new JsonObject { ["result"] = await tools.ExecuteAsync(name, args, ct) }
                        }
                    });
                }
                if (proposed == null) contents.Add(new JsonObject { ["role"] = "user", ["parts"] = replies });
            }

            proposed ??= new();
            var grounding = GroundingVerifier.Verify(proposed, tools.Grounding());
            foreach (var f in grounding.Kept) { f.CreatedBy = "INVESTIGATOR_AGENT"; f.UpdatedBy = f.CreatedBy; }

            await _cases.ReplaceFindingsAsync(id, FindingSource.AiVerified, grounding.Kept, ct);
            var at = DateTime.UtcNow;
            await _cases.RecordInvestigationAsync(id, at, model, grounding.Dropped.Select(d => new AiDropLog
            {
                LoanApplicationID = id,
                RawFinding = JsonSerializer.Serialize(d.Finding, Json),
                Reason = d.Reason.Length <= 400 ? d.Reason : d.Reason[..400],
                Model = model,
                At = at
            }).ToList(), ct);

            var result = new InvestigationResult(model, turn, tools.Calls, grounding.Kept.Count, grounding.Dropped.Count,
                                                 grounding.CitationsStripped, summary, sw.Elapsed);
            _log.LogInformation("Investigator on case {CaseId}: {Kept} kept, {Dropped} dropped, {Stripped} citations stripped, {Turns} turns, {Calls} tool calls, {Model}, {Seconds:0}s",
                id, result.Kept, result.Dropped, result.CitationsStripped, result.Turns, result.ToolCalls, model, sw.Elapsed.TotalSeconds);
            return result;
        }

        // The tool schema speaks snake_case (recommended_action); C# properties are PascalCase
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    }
}
