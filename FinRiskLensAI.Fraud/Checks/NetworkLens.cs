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
    /// For registered businesses, four more (Doc/13_TEAM_WORK_PLAN.md T7): a shared active director (DIN), a shared
    /// mobile or email, a shared address, and circular trading — businesses invoicing each other in a loop on
    /// their GSTR-1 returns. These use each customer's latest assessment only, so a business never links to itself.
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

            BusinessLinks(apps, links);

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
            // A customer by business name and Udyam number; a document-only assessment by its reference
            static string Who(NetworkApplication m) => m.Uan != null && m.DeclaredEmployer != null ? $"{m.DeclaredEmployer} ({m.Uan})" : m.ApplicantRef;
            var others = ring.Members.Where(m => m.LoanApplicationID != loanApplicationId).Select(Who).ToList();
            return new Finding
            {
                LoanApplicationID = loanApplicationId,
                Type = "FRAUD_RING",
                Category = FindingCategory.Network,
                Severity = FindingSeverity.High,
                Title = Clip($"Linked to {others.Count} other customer{(others.Count == 1 ? "" : "s")}: {string.Join(", ", others)}", 300),
                Explanation = $"{ring.Members.Count} separate customers share what unrelated businesses never do: " +
                              string.Join("; ", ring.Links.Select(l => $"{l.Kind.ToLowerInvariant()} — {l.Detail} ({l.Members.Count} customers)")) +
                              ". One operator behind several borrowers, or one fabricator supplying their documents, leaves exactly these traces.",
                RecommendedAction = "Review the linked customers together, and verify each one at source before deciding any of them.",
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

        /// <summary>The links between registered businesses: director, contact, address, circular trading.</summary>
        private static void BusinessLinks(IReadOnlyList<NetworkApplication> apps, List<RingLink> links)
        {
            var businesses = apps.Where(a => a.Uan != null && a.Entities != null)
                                 .GroupBy(a => a.Uan!, StringComparer.OrdinalIgnoreCase)
                                 .Select(g => g.MaxBy(a => a.LoanApplicationID)!)
                                 .ToList();
            string Name(NetworkApplication a) => a.DeclaredEmployer ?? a.ApplicantRef;

            foreach (var g in businesses.SelectMany(a => a.Entities!.Directors.Select(d => (App: a, Director: d)))
                                        .GroupBy(x => x.Director.Din.Trim().ToUpperInvariant()))
                Link(links, "Same director", $"{g.First().Director.Name} (DIN {g.Key}) is a director of {g.Select(x => x.App.Uan).Distinct().Count()} businesses",
                     g.Select(x => (x.App, AppEvidence(x.App, $"MCA: director {x.Director.Name}, DIN {g.Key}"))));

            foreach (var g in businesses.Where(a => Digits(a.Entities!.Mobile).Length >= 10).GroupBy(a => Digits(a.Entities!.Mobile)[^10..]))
                Link(links, "Same mobile", $"one mobile number, ending {g.Key[^4..]}, registered by separate businesses",
                     g.Select(a => (a, AppEvidence(a, $"registered mobile ending {g.Key[^4..]}"))));

            foreach (var g in businesses.Where(a => a.Entities!.Email?.Contains('@') == true).GroupBy(a => a.Entities!.Email!.Trim().ToLowerInvariant()))
                Link(links, "Same email", $"one email address, {MaskEmail(g.Key)}, registered by separate businesses",
                     g.Select(a => (a, AppEvidence(a, $"registered email {MaskEmail(g.Key)}"))));

            // An address only links when it is specific enough to mean one place: a pin code and at least 20 characters
            foreach (var g in businesses.Select(a => (App: a, Key: Squeeze(a.Entities!.Address)))
                                        .Where(x => x.Key.Length >= 20 && x.Key.Any(char.IsDigit))
                                        .GroupBy(x => x.Key))
                Link(links, "Same address", $"one address registered by separate businesses: {g.First().App.Entities!.Address}",
                     g.Select(x => (x.App, AppEvidence(x.App, $"registered address {x.App.Entities!.Address}"))));

            // Circular trading: who invoices whom among these businesses, then the groups that reach each other both ways
            var byGstin = businesses.Where(a => !string.IsNullOrWhiteSpace(a.Entities!.Gstin))
                                    .GroupBy(a => a.Entities!.Gstin!.Trim().ToUpperInvariant())
                                    .ToDictionary(g => g.Key, g => g.First());
            var sells = businesses.ToDictionary(a => a.LoanApplicationID, a => a.Entities!.InvoicedGstins
                .Select(b => byGstin.GetValueOrDefault(b.Trim().ToUpperInvariant()))
                .Where(b => b != null && b.LoanApplicationID != a.LoanApplicationID).Select(b => b!).Distinct().ToList());
            HashSet<int> Reach(int from)
            {
                var seen = new HashSet<int>();
                var todo = new Stack<int>(sells[from].Select(b => b.LoanApplicationID));
                while (todo.Count > 0)
                {
                    var n = todo.Pop();
                    if (seen.Add(n)) foreach (var m in sells[n]) todo.Push(m.LoanApplicationID);
                }
                return seen;
            }
            var reach = businesses.ToDictionary(a => a.LoanApplicationID, a => Reach(a.LoanApplicationID));
            var placed = new HashSet<int>();
            foreach (var a in businesses)
            {
                if (placed.Contains(a.LoanApplicationID)) continue;
                var loop = businesses.Where(b => reach[a.LoanApplicationID].Contains(b.LoanApplicationID) && reach[b.LoanApplicationID].Contains(a.LoanApplicationID)).ToList();
                if (loop.Count < 2) continue;
                foreach (var b in loop) placed.Add(b.LoanApplicationID);
                var ids = loop.Select(b => b.LoanApplicationID).ToHashSet();
                var edges = loop.SelectMany(b => sells[b.LoanApplicationID].Where(c => ids.Contains(c.LoanApplicationID)).Select(c => $"{Name(b)} → {Name(c)}"));
                Link(links, "Circular trading", $"{loop.Count} businesses invoice each other in a loop on their GST returns: {string.Join("; ", edges)}",
                     loop.Select(b => (b, AppEvidence(b, "GSTR-1 invoices to " +
                         string.Join(", ", sells[b.LoanApplicationID].Where(c => ids.Contains(c.LoanApplicationID)).Select(Name))))));
            }
        }

        private static Evidence AppEvidence(NetworkApplication a, string note) => new()
        {
            Kind = EvidenceKind.App, RefId = a.LoanApplicationID, Note = Clip(note, 400)
        };

        private static string Digits(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());
        private static string Squeeze(string? s) => new((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        private static string MaskEmail(string email)
        {
            var at = email.IndexOf('@');
            return at <= 1 ? email : email[0] + new string('*', Math.Min(at - 1, 6)) + email[at..];
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
