using System.Globalization;
using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Fraud.Ai
{
    /// <summary>A finding as the Investigator proposed it — untrusted until verified.</summary>
    public sealed class ProposedFinding
    {
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Title { get; set; }
        public string? Explanation { get; set; }
        public string? RecommendedAction { get; set; }
        public List<ProposedEvidence>? Evidence { get; set; }
        /// <summary>Knowledge chunk ids the explanation or action relies on.</summary>
        public List<long>? Citations { get; set; }
    }

    public sealed class ProposedEvidence
    {
        /// <summary>"row" or "field".</summary>
        public string? Kind { get; set; }
        public long Id { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>What the model was actually shown during the run — the only things it may rely on.</summary>
    public sealed class GroundingContext
    {
        public int LoanApplicationID { get; init; }
        public IReadOnlyDictionary<long, StatementRow> Rows { get; init; } = new Dictionary<long, StatementRow>();
        public IReadOnlyDictionary<long, DocField> Fields { get; init; } = new Dictionary<long, DocField>();
        /// <summary>Declared figures and verification facts the tools returned.</summary>
        public ISet<decimal> KnownAmounts { get; init; } = new HashSet<decimal>();
        /// <summary>Chunk id → breadcrumb, for every chunk a knowledge search returned.</summary>
        public IReadOnlyDictionary<long, string> RetrievedChunks { get; init; } = new Dictionary<long, string>();
    }

    public sealed record GroundingResult(List<Finding> Kept, List<(ProposedFinding Finding, string Reason)> Dropped, int CitationsStripped);

    /// <summary>
    /// Step ⑥ (Doc/10_FRAUD_LENS_BUILD.md): nothing the model says reaches an investigator
    /// unless it is anchored in this application's own data. A finding is dropped — not
    /// softened — when its type is outside the AI catalogue, when it cites a row or field that
    /// does not exist or belongs to someone else, or when it quotes a rupee amount that is not
    /// a cited value, the cited total, a declared figure or a verification fact it was shown.
    /// Citations to policy the search never returned are stripped; the finding stands without them.
    /// </summary>
    public static class GroundingVerifier
    {
        /// <summary>What the AI may raise — everything the deterministic checks already cover is theirs.</summary>
        public static readonly IReadOnlyDictionary<string, FindingCategory> Catalogue = new Dictionary<string, FindingCategory>
        {
            ["UNDECLARED_EMI"] = FindingCategory.HiddenLiabilities,
            ["LOAN_STACKING"] = FindingCategory.HiddenLiabilities,
            ["GAMBLING_CRYPTO_EXPOSURE"] = FindingCategory.Behavioural,
            ["BOUNCES"] = FindingCategory.Behavioural,
            ["SALARY_SOURCE_MISMATCH"] = FindingCategory.IncomeInflation,
            ["INCOME_MISMATCH"] = FindingCategory.CrossDocument,
            ["IDENTITY_MISMATCH"] = FindingCategory.CrossDocument,
            ["IMPLAUSIBLE_EMPLOYER"] = FindingCategory.Fabrication
        };

        // ₹1,23,456.78 · Rs. 45,000 · INR 2500 — full-rupee figures only; the prompt forbids lakh/crore shorthand
        private static readonly Regex Rupees = new(@"(?:₹|Rs\.?|INR)\s?(\d[\d,]*(?:\.\d{1,2})?)", RegexOptions.IgnoreCase);

        public static GroundingResult Verify(IEnumerable<ProposedFinding> proposed, GroundingContext ctx)
        {
            var kept = new List<Finding>();
            var dropped = new List<(ProposedFinding, string)>();
            var keys = new HashSet<string>();
            int stripped = 0;

            foreach (var p in proposed)
            {
                var type = p.Type?.Trim().ToUpperInvariant() ?? "";
                if (!Catalogue.TryGetValue(type, out var category)) { dropped.Add((p, $"type '{p.Type}' is not in the AI catalogue")); continue; }
                if (string.IsNullOrWhiteSpace(p.Title) || string.IsNullOrWhiteSpace(p.Explanation)) { dropped.Add((p, "no title or explanation")); continue; }

                var evidence = p.Evidence ?? new();
                if (evidence.Count == 0) { dropped.Add((p, "no evidence cited")); continue; }

                var rows = new List<StatementRow>();
                var fields = new List<DocField>();
                string? bad = null;
                foreach (var e in evidence)
                {
                    var kind = e.Kind?.Trim().ToLowerInvariant();
                    if (kind == "row" && ctx.Rows.TryGetValue(e.Id, out var r)) rows.Add(r);
                    else if (kind == "field" && ctx.Fields.TryGetValue(e.Id, out var f)) fields.Add(f);
                    else { bad = $"cites {e.Kind} {e.Id}, which is not in this application's documents"; break; }
                }
                if (bad != null) { dropped.Add((p, bad)); continue; }

                // Every rupee figure in what the investigator will read must be traceable
                var allowed = new List<decimal>(ctx.KnownAmounts);
                foreach (var r in rows) allowed.AddRange(new[] { r.Debit, r.Credit, r.Balance }.Where(v => v != null).Select(v => v!.Value));
                allowed.AddRange(fields.Where(f => f.NumValue != null).Select(f => f.NumValue!.Value));
                allowed.Add(rows.Sum(r => r.Debit ?? 0));
                allowed.Add(rows.Sum(r => r.Credit ?? 0));
                // "₹74,000 to Swift" from rows citing two lenders: each counterparty's own total counts
                foreach (var g in rows.GroupBy(Party))
                {
                    allowed.Add(g.Sum(r => r.Debit ?? 0));
                    allowed.Add(g.Sum(r => r.Credit ?? 0));
                }

                var unverified = Amounts(p.Title + " " + p.Explanation).FirstOrDefault(a => !allowed.Any(x => Math.Abs(x - a) <= 1m));
                if (unverified != 0) { dropped.Add((p, $"quotes ₹{unverified.ToString("N2", CultureInfo.InvariantCulture)}, which matches no cited value, total or fact")); continue; }

                var docId = rows.Select(r => (int?)r.CaseDocumentID).Concat(fields.Select(f => (int?)f.CaseDocumentID)).First();
                var key = $"{type}:ai:doc{docId}";
                if (!keys.Add(key)) { dropped.Add((p, "duplicates another finding of the same type on the same document")); continue; }

                var citations = (p.Citations ?? new()).Distinct().ToList();
                var valid = citations.Where(ctx.RetrievedChunks.ContainsKey).ToList();
                stripped += citations.Count - valid.Count;

                var finding = new Finding
                {
                    LoanApplicationID = ctx.LoanApplicationID,
                    Type = type,
                    Category = category,
                    Severity = Enum.TryParse<FindingSeverity>(p.Severity, ignoreCase: true, out var sev) ? sev : FindingSeverity.Medium,
                    Title = Clip(p.Title.Trim(), 300),
                    Explanation = p.Explanation.Trim(),
                    RecommendedAction = string.IsNullOrWhiteSpace(p.RecommendedAction) ? null : p.RecommendedAction.Trim(),
                    DetectedBy = FindingSource.AiVerified,
                    DedupKey = key,
                    Evidence = rows.Select(r => new Evidence
                    {
                        Kind = EvidenceKind.Row, RefId = r.StatementRowID, CaseDocumentID = r.CaseDocumentID, PageNo = r.PageNo, Box = r.Box,
                        Note = Clip($"row {r.RowNo}, {r.TxnDate:dd MMM yyyy}: {r.Description}", 400)
                    })
                    .Concat(fields.Select(f => new Evidence
                    {
                        Kind = EvidenceKind.Field, RefId = f.DocFieldID, CaseDocumentID = f.CaseDocumentID, PageNo = f.PageNo, Box = f.Box,
                        Note = Clip($"{f.Name}: {f.Value}", 400)
                    }))
                    .Take(25)
                    .Concat(valid.Select(c => new Evidence { Kind = EvidenceKind.Policy, RefId = c, Note = Clip(ctx.RetrievedChunks[c], 400) }))
                    .ToList()
                };
                kept.Add(finding);
            }
            return new GroundingResult(kept, dropped, stripped);
        }

        private static string Party(StatementRow r)
            => (r.CounterpartyName ?? r.CounterpartyAcct ?? r.Description).Trim().ToUpperInvariant();

        public static IEnumerable<decimal> Amounts(string text)
            => Rupees.Matches(text)
                     .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0)
                     .Where(v => v > 0);

        private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
