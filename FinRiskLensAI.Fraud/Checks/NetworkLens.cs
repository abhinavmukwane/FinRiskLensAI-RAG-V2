using System.Globalization;
using FinRiskLensAI.Core.Models.Cases;

namespace FinRiskLensAI.Fraud.Checks
{
    /// <summary>
    /// Fraud rings (typology T-08): one fabricator supplying documents to many applicants leaves
    /// traces genuine applicants never share. Three links, each deterministic:
    /// <list type="bullet">
    /// <item>the same file, byte for byte, in separate applications;</item>
    /// <item>one template — the same producing software, creator and fonts — on documents claiming
    ///       different issuers (every statement from one bank shares its template; two banks never do);</item>
    /// <item>one account paying "salary" to applicants who name different employers (colleagues share
    ///       a payroll account; strangers with different employers do not).</item>
    /// </list>
    /// Linked applications are grouped by union-find; a ring is any group of two or more.
    /// </summary>
    public static class NetworkLens
    {
        private const int MaxEvidence = 25;
        private static readonly CultureInfo InIndia = new("en-IN");

        public static IReadOnlyList<Ring> Find(IReadOnlyList<NetworkApplication> apps)
        {
            var links = new List<RingLink>();
            var docs = apps.SelectMany(a => a.Documents.Select(d => (App: a, Doc: d))).ToList();

            foreach (var g in docs.Where(x => x.Doc.Sha256.Length > 0).GroupBy(x => Convert.ToHexString(x.Doc.Sha256)))
                Link(links, "Same file", $"“{g.First().Doc.FileName}” uploaded byte for byte",
                     g.Select(x => (x.App, DocEvidence(x.Doc, "byte-identical file"))));

            foreach (var g in docs.Where(x => x.Doc.PdfProducer != null && !string.IsNullOrWhiteSpace(x.Doc.ClaimedIssuer))
                                  .GroupBy(x => (x.Doc.PdfProducer, x.Doc.PdfCreator, x.Doc.FontSet)))
            {
                var issuers = g.Select(x => x.Doc.ClaimedIssuer!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (issuers.Count < 2) continue;
                Link(links, "Same template",
                     $"made by “{g.Key.PdfProducer}” with the same fonts, yet claiming {issuers.Count} issuers: {string.Join(", ", issuers)}",
                     g.Select(x => (x.App, DocEvidence(x.Doc, $"claims to come from {x.Doc.ClaimedIssuer}"))));
            }

            foreach (var g in apps.SelectMany(a => a.SalaryCredits.Select(r => (App: a, Row: r)))
                                  .GroupBy(x => new string(x.Row.CounterpartyAcct!.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant()))
            {
                var employers = g.Select(x => x.App.DeclaredEmployer?.Trim()).Where(e => !string.IsNullOrEmpty(e))
                                 .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (employers.Count < 2) continue;
                Link(links, "Same salary account",
                     $"account {g.First().Row.CounterpartyAcct} pays the salary of applicants naming {employers.Count} different employers: {string.Join(", ", employers)}",
                     g.Select(x => (x.App, RowEvidence(x.Row))));
            }

            // Union-find: applications joined directly or through each other are one ring
            var parent = apps.ToDictionary(a => a.LoanApplicationID, a => a.LoanApplicationID);
            int Root(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            foreach (var l in links)
            {
                var first = l.Members.Keys.First();
                foreach (var id in l.Members.Keys) parent[Root(id)] = Root(first);
            }

            return apps.GroupBy(a => Root(a.LoanApplicationID))
                       .Where(g => g.Count() >= 2)
                       .Select(g =>
                       {
                           var root = g.Key;
                           return new Ring(g.OrderBy(a => a.ApplicantRef).ToList(),
                                           links.Where(l => Root(l.Members.Keys.First()) == root).ToList());
                       })
                       .OrderByDescending(r => r.Members.Count)
                       .ToList();
        }

        /// <summary>The ring's finding for one member: who it is linked to, by what, and that member's own evidence.</summary>
        public static Finding FindingFor(Ring ring, int loanApplicationId)
        {
            var others = ring.Members.Where(m => m.LoanApplicationID != loanApplicationId).Select(m => m.ApplicantRef).ToList();
            return new Finding
            {
                LoanApplicationID = loanApplicationId,
                Type = "FRAUD_RING",
                Category = FindingCategory.Network,
                Severity = FindingSeverity.High,
                Title = Clip($"Linked to {others.Count} other application{(others.Count == 1 ? "" : "s")}: {string.Join(", ", others)}", 300),
                Explanation = $"{ring.Members.Count} separate applications share what unrelated applicants never do: " +
                              string.Join("; ", ring.Links.Select(l => $"{l.Kind.ToLowerInvariant()} — {l.Detail} ({l.Members.Count} applications)")) +
                              ". One fabricator supplying documents to many applicants leaves exactly these traces.",
                RecommendedAction = "Review the linked applications together, and verify each one at source before deciding any of them.",
                DetectedBy = FindingSource.Deterministic,
                DedupKey = "FRAUD_RING",
                // Fresh copies: an evidence row belongs to one finding
                Evidence = ring.Links.Where(l => l.Members.ContainsKey(loanApplicationId))
                                     .SelectMany(l => l.Members[loanApplicationId])
                                     .Take(MaxEvidence)
                                     .Select(e => new Evidence { Kind = e.Kind, RefId = e.RefId, CaseDocumentID = e.CaseDocumentID,
                                                                 PageNo = e.PageNo, Box = e.Box, Note = e.Note })
                                     .ToList()
            };
        }

        private static void Link(List<RingLink> links, string kind, string detail, IEnumerable<(NetworkApplication App, Evidence Evidence)> members)
        {
            var byApp = members.GroupBy(m => m.App.LoanApplicationID)
                               .ToDictionary(g => g.Key, g => (IReadOnlyList<Evidence>)g.Select(m => m.Evidence).ToList());
            if (byApp.Count >= 2) links.Add(new RingLink(kind, detail, byApp));
        }

        private static Evidence DocEvidence(CaseDocument d, string note) => new()
        {
            Kind = EvidenceKind.Doc, RefId = d.CaseDocumentID, CaseDocumentID = d.CaseDocumentID, PageNo = 1,
            Note = Clip($"{d.FileName}: {note}", 400)
        };

        private static Evidence RowEvidence(StatementRow r) => new()
        {
            Kind = EvidenceKind.Row, RefId = r.StatementRowID, CaseDocumentID = r.CaseDocumentID, PageNo = r.PageNo, Box = r.Box,
            Note = Clip($"row {r.RowNo}, {r.TxnDate:dd MMM yyyy}: salary ₹{r.Credit?.ToString("N0", InIndia)} from account {r.CounterpartyAcct}", 400)
        };

        private static string Clip(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
