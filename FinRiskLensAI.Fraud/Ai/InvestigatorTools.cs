using System.Text.Json.Nodes;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.Knowledge;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Knowledge;

namespace FinRiskLensAI.Fraud.Ai
{
    /// <summary>
    /// The Investigator's tools. Every one is bound to a single application's data, loaded
    /// before the run — the model can ask for anything, but there is nothing else to reach.
    /// Each call also records what the model was shown, which is exactly what the grounding
    /// verifier later allows it to rely on.
    /// </summary>
    public sealed class InvestigatorTools
    {
        private const int MaxRows = 120;

        private readonly LoanApplication _app;
        private readonly List<StatementRow> _rows;
        private readonly List<DocField> _fields;
        private readonly IReadOnlyList<Finding> _deterministic;
        private readonly IBlobAnalysisService? _facts;
        private readonly IKnowledgeService _knowledge;

        public HashSet<decimal> KnownAmounts { get; } = new();
        public Dictionary<long, string> RetrievedChunks { get; } = new();
        public int Calls { get; private set; }

        public InvestigatorTools(LoanApplication app, IEnumerable<StatementRow> rows, IEnumerable<DocField> fields,
                                 IReadOnlyList<Finding> deterministic, IBlobAnalysisService? facts, IKnowledgeService knowledge)
        {
            _app = app;
            _rows = rows.OrderBy(r => r.CaseDocumentID).ThenBy(r => r.RowNo).ToList();
            _fields = fields.ToList();
            _deterministic = deterministic;
            _facts = facts;
            _knowledge = knowledge;
            if (app.DeclaredIncome is { } i) KnownAmounts.Add(i);
            if (app.DeclaredEmis is { } e) KnownAmounts.Add(e);
        }

        public GroundingContext Grounding() => new()
        {
            LoanApplicationID = _app.LoanApplicationID,
            Rows = _rows.ToDictionary(r => r.StatementRowID),
            Fields = _fields.ToDictionary(f => f.DocFieldID),
            KnownAmounts = KnownAmounts,
            RetrievedChunks = RetrievedChunks
        };

        public async Task<JsonNode> ExecuteAsync(string name, JsonObject args, CancellationToken ct)
        {
            Calls++;
            return name switch
            {
                "get_case_overview" => Overview(),
                "get_statement_rows" => Rows(args),
                "get_counterparty_summary" => Counterparties(args),
                "get_document_fields" => Fields(args),
                "get_verification_facts" => await FactsAsync(ct),
                "search_policy" => await SearchAsync(args, ct),
                _ => new JsonObject { ["error"] = $"unknown tool {name}" }
            };
        }

        private JsonNode Overview() => new JsonObject
        {
            ["application"] = new JsonObject
            {
                ["reference"] = _app.ApplicantRef,
                ["product"] = _app.ProductType.ToString(),
                ["declared_monthly_income"] = _app.DeclaredIncome,
                ["declared_monthly_emis"] = _app.DeclaredEmis,
                ["declared_employer_or_business"] = _app.DeclaredEmployer,
                ["udyam_number"] = _app.Uan,
                ["submitted_at"] = _app.SubmittedAt.ToString("yyyy-MM-dd")
            },
            ["documents"] = new JsonArray(_app.Documents.Select(d =>
            {
                var rows = _rows.Where(r => r.CaseDocumentID == d.CaseDocumentID).ToList();
                return (JsonNode)new JsonObject
                {
                    ["document_id"] = d.CaseDocumentID,
                    ["file"] = d.FileName,
                    ["doc_type"] = d.DocType.ToString(),
                    ["claimed_issuer"] = d.ClaimedIssuer,
                    ["pages"] = d.PageCount,
                    ["statement_rows"] = rows.Count,
                    ["period"] = rows.Count == 0 ? null : $"{rows.Min(r => r.TxnDate):yyyy-MM-dd} to {rows.Max(r => r.TxnDate):yyyy-MM-dd}",
                    ["fields"] = _fields.Count(f => f.CaseDocumentID == d.CaseDocumentID)
                };
            }).ToArray()),
            ["already_found_by_deterministic_checks"] = new JsonArray(_deterministic.Select(f => (JsonNode)$"{f.Type}: {f.Title}").ToArray()),
            ["verification_sources_linked"] = _app.Uan != null && _facts != null
        };

        private JsonNode Rows(JsonObject a)
        {
            IEnumerable<StatementRow> q = _rows;
            if (Int(a, "document_id") is { } doc) q = q.Where(r => r.CaseDocumentID == doc);
            var dir = Str(a, "direction")?.ToLowerInvariant();
            if (dir == "debit") q = q.Where(r => r.Debit > 0);
            if (dir == "credit") q = q.Where(r => r.Credit > 0);
            if (Num(a, "min_amount") is { } min) q = q.Where(r => (r.Debit ?? r.Credit ?? 0) >= min);
            if (Str(a, "text_contains") is { Length: > 0 } text)
                q = q.Where(r => r.Description.Contains(text, StringComparison.OrdinalIgnoreCase)
                              || (r.CounterpartyName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
            if (Str(a, "date_from") is { } from && DateOnly.TryParse(from, out var f)) q = q.Where(r => r.TxnDate >= f);
            if (Str(a, "date_to") is { } to && DateOnly.TryParse(to, out var t)) q = q.Where(r => r.TxnDate <= t);

            var all = q.ToList();
            var limit = Math.Clamp(Int(a, "limit") ?? MaxRows, 1, MaxRows);
            return new JsonObject
            {
                ["matched"] = all.Count,
                ["returned"] = Math.Min(all.Count, limit),
                ["rows"] = new JsonArray(all.Take(limit).Select(r => (JsonNode)new JsonObject
                {
                    ["row_id"] = r.StatementRowID,
                    ["document_id"] = r.CaseDocumentID,
                    ["page"] = r.PageNo,
                    ["date"] = r.TxnDate.ToString("yyyy-MM-dd"),
                    ["description"] = r.Description,
                    ["debit"] = r.Debit,
                    ["credit"] = r.Credit,
                    ["balance"] = r.Balance,
                    ["counterparty"] = r.CounterpartyName ?? r.CounterpartyAcct
                }).ToArray())
            };
        }

        /// <summary>Recurring payees and payers — a monthly fixed debit to one lender is what an EMI looks like.</summary>
        private JsonNode Counterparties(JsonObject a)
        {
            IEnumerable<StatementRow> q = _rows;
            if (Int(a, "document_id") is { } doc) q = q.Where(r => r.CaseDocumentID == doc);

            var groups = q.GroupBy(r => (Party: Party(r), Debit: r.Debit > 0))
                .Select(g => new
                {
                    g.Key.Party,
                    Direction = g.Key.Debit ? "debit" : "credit",
                    Count = g.Count(),
                    Total = g.Sum(r => r.Debit ?? r.Credit ?? 0),
                    Months = g.Select(r => (r.TxnDate.Year, r.TxnDate.Month)).Distinct().Count(),
                    Typical = g.GroupBy(r => r.Debit ?? r.Credit ?? 0).OrderByDescending(x => x.Count()).First().Key,
                    First = g.Min(r => r.TxnDate),
                    Last = g.Max(r => r.TxnDate),
                    Ids = g.Select(r => r.StatementRowID).Take(6).ToArray()
                })
                .OrderByDescending(x => x.Total).Take(40);

            return new JsonObject
            {
                ["counterparties"] = new JsonArray(groups.Select(x => (JsonNode)new JsonObject
                {
                    ["counterparty"] = x.Party,
                    ["direction"] = x.Direction,
                    ["transactions"] = x.Count,
                    ["total"] = x.Total,
                    ["months_active"] = x.Months,
                    ["most_common_amount"] = x.Typical,
                    ["first_date"] = x.First.ToString("yyyy-MM-dd"),
                    ["last_date"] = x.Last.ToString("yyyy-MM-dd"),
                    ["sample_row_ids"] = new JsonArray(x.Ids.Select(i => (JsonNode)i).ToArray())
                }).ToArray())
            };
        }

        private JsonNode Fields(JsonObject a)
        {
            IEnumerable<DocField> q = _fields;
            if (Int(a, "document_id") is { } doc) q = q.Where(f => f.CaseDocumentID == doc);
            return new JsonObject
            {
                ["fields"] = new JsonArray(q.Select(f => (JsonNode)new JsonObject
                {
                    ["field_id"] = f.DocFieldID,
                    ["document_id"] = f.CaseDocumentID,
                    ["name"] = f.Name,
                    ["value"] = f.Value,
                    ["numeric_value"] = f.NumValue
                }).ToArray())
            };
        }

        /// <summary>
        /// What the registry and Account Aggregator sources say about the business — the truth the
        /// uploads are checked against. Rounded to whole rupees; those exact figures become quotable.
        /// </summary>
        private async Task<JsonNode> FactsAsync(CancellationToken ct)
        {
            if (_app.Uan == null || _facts == null)
                return new JsonObject { ["available"] = false, ["reason"] = "No Udyam number is linked to this application." };

            var facts = await _facts.GetFactsAsync(_app.Uan, ct);
            if (facts == null)
                return new JsonObject { ["available"] = false, ["reason"] = "No verification source has been fetched for this Udyam number." };

            var x = facts.Features;
            decimal Rs(double v) { var d = Math.Round((decimal)v); KnownAmounts.Add(d); return d; }

            return new JsonObject
            {
                ["available"] = true,
                ["note"] = "ITR gross total income is close to profit — compare turnover with itr.business_turnover, never with gross total income.",
                ["gst"] = new JsonObject
                {
                    ["registered"] = x.HasGst, ["registration_active"] = x.GstRegistrationActive,
                    ["monthly_avg_turnover"] = Rs(x.GstMonthlyAvgTurnover), ["annualised_turnover"] = Rs(x.GstAnnualisedTurnover),
                    ["filing_regularity_0_to_1"] = Math.Round(x.GstFilingRegularity, 2)
                },
                ["itr"] = new JsonObject
                {
                    ["filed"] = x.HasItr, ["assessment_year"] = x.ItrAssessmentYear, ["years_filed"] = x.ItrYearsFiled,
                    ["business_turnover"] = Rs(x.ItrBusinessTurnover), ["gross_total_income"] = Rs(x.ItrLatestIncome),
                    ["tax_audit_applicable"] = x.ItrAuditApplicable, ["tax_audit_completed"] = x.ItrAuditCompleted
                },
                ["account_aggregator"] = new JsonObject
                {
                    ["linked"] = x.HasAa, ["months_covered"] = x.AaMonthsCovered,
                    ["monthly_avg_bank_credit"] = Rs(x.AaMonthlyAvgBankCredit), ["avg_monthly_balance"] = Rs(x.AaAvgMonthlyBalance),
                    ["emi_monthly_outflow"] = Rs(x.AaEmiMonthlyOutflow), ["bounces"] = x.AaBounceCount,
                    ["ecs_nach_returns"] = x.AaEcsNachReturnCount, ["cheque_returns"] = x.AaChequeReturnCount
                }
            };
        }

        private async Task<JsonNode> SearchAsync(JsonObject a, CancellationToken ct)
        {
            var query = Str(a, "query");
            if (string.IsNullOrWhiteSpace(query)) return new JsonObject { ["error"] = "query is required" };

            var corpora = (a["corpora"] as JsonArray)?.Select(n => n?.GetValue<string>())
                .Select(s => Enum.TryParse<KnowledgeCorpus>(s, true, out var c) ? c : (KnowledgeCorpus?)null)
                .Where(c => c != null).Select(c => c!.Value).ToList();
            if (corpora == null || corpora.Count == 0) corpora = new() { KnowledgeCorpus.Policy, KnowledgeCorpus.Typology, KnowledgeCorpus.Regulation };

            var hits = await _knowledge.SearchAsync(query, corpora, topK: 4, ct);
            foreach (var h in hits) RetrievedChunks[h.ChunkId] = h.Breadcrumb ?? h.Title;
            return new JsonObject
            {
                ["results"] = new JsonArray(hits.Select(h => (JsonNode)new JsonObject
                {
                    ["chunk_id"] = h.ChunkId,
                    ["source"] = h.Breadcrumb,
                    ["relevance"] = h.Relevance,
                    ["text"] = h.Text.Length <= 700 ? h.Text : h.Text[..700] + "…"
                }).ToArray())
            };
        }

        private static string Party(StatementRow r)
        {
            if (!string.IsNullOrWhiteSpace(r.CounterpartyName)) return r.CounterpartyName.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(r.CounterpartyAcct)) return r.CounterpartyAcct.Trim();
            // No counterparty printed: the narration without digits, first few words, groups recurring lines
            var words = new string(r.Description.Where(c => !char.IsDigit(c)).ToArray())
                .Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(4);
            return string.Join(' ', words).ToUpperInvariant();
        }

        private static string? Str(JsonObject a, string k) => a[k] is JsonValue v && v.TryGetValue<string>(out var s) ? s.Trim() : null;
        private static int? Int(JsonObject a, string k) => a[k] is JsonValue v ? (v.TryGetValue<int>(out var i) ? i : v.TryGetValue<double>(out var d) ? (int)d : null) : null;
        private static decimal? Num(JsonObject a, string k) => a[k] is JsonValue v && v.TryGetValue<double>(out var d) ? (decimal)d : null;

        // ── declarations (Vertex OpenAPI subset) ────────────────────────────────────

        public static JsonArray Declarations()
        {
            static JsonObject P(string type, string description) => new() { ["type"] = type, ["description"] = description };
            static JsonObject Fn(string name, string description, JsonObject? props = null, params string[] required)
            {
                var fn = new JsonObject { ["name"] = name, ["description"] = description };
                if (props != null)
                    fn["parameters"] = new JsonObject
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = props,
                        ["required"] = new JsonArray(required.Select(r => (JsonNode)r).ToArray())
                    };
                return fn;
            }

            var findingTypes = new JsonArray(GroundingVerifier.Catalogue.Keys.Select(k => (JsonNode)k).ToArray());

            return new JsonArray(
                Fn("get_case_overview", "The application's declared figures, its documents (ids, types, periods) and what the deterministic checks already found."),
                Fn("get_statement_rows", "Bank-statement rows of this application, filtered. Returns at most 120 rows plus the total matched.",
                    new JsonObject
                    {
                        ["document_id"] = P("INTEGER", "Only this document."),
                        ["direction"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("debit", "credit") },
                        ["min_amount"] = P("NUMBER", "Only rows of at least this amount."),
                        ["text_contains"] = P("STRING", "Case-insensitive match on narration or counterparty."),
                        ["date_from"] = P("STRING", "YYYY-MM-DD"),
                        ["date_to"] = P("STRING", "YYYY-MM-DD"),
                        ["limit"] = P("INTEGER", "Up to 120.")
                    }),
                Fn("get_counterparty_summary", "Payers and payees with totals, months active and the most common amount — recurring fixed debits to a lender look like EMIs.",
                    new JsonObject { ["document_id"] = P("INTEGER", "Only this document.") }),
                Fn("get_document_fields", "Named fields from salary slips, ITRs and KYC documents of this application.",
                    new JsonObject { ["document_id"] = P("INTEGER", "Only this document.") }),
                Fn("get_verification_facts", "What GST returns, the filed ITR and Account Aggregator bank data say about the business behind the linked Udyam number."),
                Fn("search_policy", "Search the lender's credit policy, fraud typologies and regulations. Cite the chunk_id of any clause you rely on.",
                    new JsonObject
                    {
                        ["query"] = P("STRING", "What to look for, in plain words."),
                        ["corpora"] = new JsonObject
                        {
                            ["type"] = "ARRAY",
                            ["items"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("Policy", "Typology", "Regulation") }
                        }
                    }, "query"),
                Fn("submit_findings", "Submit the investigation result. Call exactly once, at the end. An empty list means nothing beyond the deterministic findings.",
                    new JsonObject
                    {
                        ["summary"] = P("STRING", "Two or three sentences for the investigator."),
                        ["findings"] = new JsonObject
                        {
                            ["type"] = "ARRAY",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "OBJECT",
                                ["required"] = new JsonArray("type", "severity", "title", "explanation", "evidence"),
                                ["properties"] = new JsonObject
                                {
                                    ["type"] = new JsonObject { ["type"] = "STRING", ["enum"] = findingTypes },
                                    ["severity"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("Low", "Medium", "High") },
                                    ["title"] = P("STRING", "One line."),
                                    ["explanation"] = P("STRING", "What the evidence shows, with exact rupee amounts."),
                                    ["recommended_action"] = P("STRING", "What the investigator should do, naming the policy clause."),
                                    ["evidence"] = new JsonObject
                                    {
                                        ["type"] = "ARRAY",
                                        ["items"] = new JsonObject
                                        {
                                            ["type"] = "OBJECT",
                                            ["required"] = new JsonArray("kind", "id"),
                                            ["properties"] = new JsonObject
                                            {
                                                ["kind"] = new JsonObject { ["type"] = "STRING", ["enum"] = new JsonArray("row", "field") },
                                                ["id"] = P("INTEGER", "row_id or field_id from a tool result."),
                                                ["note"] = P("STRING", "Why this row or field matters.")
                                            }
                                        }
                                    },
                                    ["citations"] = new JsonObject { ["type"] = "ARRAY", ["items"] = new JsonObject { ["type"] = "INTEGER" } }
                                }
                            }
                        }
                    }, "summary", "findings"));
        }
    }
}
