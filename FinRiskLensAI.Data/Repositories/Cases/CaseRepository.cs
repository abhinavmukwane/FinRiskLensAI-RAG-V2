using FinRiskLensAI.Core.Interfaces.IRepositories.Cases;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Data.DbContextEDMX;
using Microsoft.EntityFrameworkCore;

namespace FinRiskLensAI.Data.Repositories.Cases
{
    public class CaseRepository : ICaseRepository
    {
        private readonly ApplicationDbContext _context;

        public CaseRepository(ApplicationDbContext context) => _context = context;

        public async Task<int> AddAsync(LoanApplication application, CancellationToken ct = default)
        {
            _context.LoanApplications.Add(application);
            await _context.SaveChangesAsync(ct);
            return application.LoanApplicationID;
        }

        public async Task<IReadOnlyList<CaseListItem>> ListAsync(CancellationToken ct = default)
            => await _context.LoanApplications.AsNoTracking()
                   .OrderByDescending(a => a.SubmittedAt).ThenByDescending(a => a.LoanApplicationID)
                   .Select(a => new CaseListItem(a.LoanApplicationID, a.ApplicantRef, a.ProductType, a.Uan,
                                                 a.Status, a.SubmittedAt, a.Documents.Count))
                   .ToListAsync(ct);

        public Task<LoanApplication?> GetAsync(int loanApplicationId, CancellationToken ct = default)
            => _context.LoanApplications.AsNoTracking()
                   .Include(a => a.Documents.OrderBy(d => d.CaseDocumentID))
                   .FirstOrDefaultAsync(a => a.LoanApplicationID == loanApplicationId, ct);

        public Task<CaseDocument?> GetDocumentAsync(int loanApplicationId, int caseDocumentId, CancellationToken ct = default)
            => _context.CaseDocuments.AsNoTracking()
                   .FirstOrDefaultAsync(d => d.CaseDocumentID == caseDocumentId
                                          && d.LoanApplicationID == loanApplicationId, ct);

        public async Task<IReadOnlyDictionary<int, IReadOnlyList<int>>> FindSharedFilesAsync(
            int loanApplicationId, CancellationToken ct = default)
        {
            // Self-join on the hash: this application's documents against everyone else's.
            var pairs = await (
                from mine in _context.CaseDocuments
                join other in _context.CaseDocuments on mine.Sha256 equals other.Sha256
                where mine.LoanApplicationID == loanApplicationId
                   && other.LoanApplicationID != loanApplicationId
                select new { mine.CaseDocumentID, other.LoanApplicationID })
                .Distinct()
                .ToListAsync(ct);

            return pairs.GroupBy(p => p.CaseDocumentID)
                        .ToDictionary(g => g.Key,
                                      g => (IReadOnlyList<int>)g.Select(p => p.LoanApplicationID).OrderBy(id => id).ToList());
        }

        // ponytail: every application in memory on each call — fine to a few thousand cases; past that,
        // write entity links (file hash, template, payer account) to a table at extraction and query those
        public async Task<IReadOnlyList<NetworkApplication>> GetNetworkAsync(CancellationToken ct = default)
        {
            var apps = await _context.LoanApplications.AsNoTracking().Include(a => a.Documents).ToListAsync(ct);
            var salary = await (
                from r in _context.StatementRows.AsNoTracking()
                join d in _context.CaseDocuments on r.CaseDocumentID equals d.CaseDocumentID
                where r.Credit > 0 && r.CounterpartyAcct != null && r.Description.ToUpper().Contains("SALARY")
                select new { d.LoanApplicationID, Row = r })
                .ToListAsync(ct);
            var byApp = salary.ToLookup(x => x.LoanApplicationID, x => x.Row);

            return apps.Select(a => new NetworkApplication(a.LoanApplicationID, a.ApplicantRef, a.DeclaredEmployer,
                                                           a.Documents, byApp[a.LoanApplicationID].ToList(), a.Uan))
                       .ToList();
        }

        public Task<int?> FindByUanAsync(string uan, CancellationToken ct = default)
            => _context.LoanApplications.AsNoTracking()
                   .Where(a => a.Uan == uan)
                   .OrderByDescending(a => a.LoanApplicationID)
                   .Select(a => (int?)a.LoanApplicationID)
                   .FirstOrDefaultAsync(ct);

        public async Task AddDocumentsAsync(int loanApplicationId, IReadOnlyList<CaseDocument> documents, CancellationToken ct = default)
        {
            foreach (var d in documents) d.LoanApplicationID = loanApplicationId;
            _context.CaseDocuments.AddRange(documents);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyDictionary<string, RiskSummary>> GetRiskSummariesAsync(CancellationToken ct = default)
        {
            var apps = await _context.LoanApplications.AsNoTracking()
                .Where(a => a.Uan != null)
                .Select(a => new
                {
                    a.LoanApplicationID, a.Uan, a.Status, a.InvestigatedAt,
                    Documents = a.Documents.Count,
                    Pending = a.Documents.Count(d => d.ExtractStatus == ExtractStatus.Pending)
                })
                .ToListAsync(ct);
            var open = (await _context.Findings.AsNoTracking()
                    .Where(f => f.ReviewStatus == ReviewStatus.Open)
                    .GroupBy(f => new { f.LoanApplicationID, f.Severity })
                    .Select(g => new { g.Key.LoanApplicationID, g.Key.Severity, Count = g.Count() })
                    .ToListAsync(ct))
                .ToLookup(x => x.LoanApplicationID);

            return apps.GroupBy(a => a.Uan!)
                .Select(g => g.MaxBy(a => a.LoanApplicationID)!)
                .ToDictionary(a => a.Uan!, a =>
                {
                    int Open(FindingSeverity s) => open[a.LoanApplicationID].Where(x => x.Severity == s).Sum(x => x.Count);
                    var (high, medium, low) = (Open(FindingSeverity.High), Open(FindingSeverity.Medium), Open(FindingSeverity.Low));
                    var assessing = a.Pending > 0 || a.Status == CaseStatus.Processing;
                    return new RiskSummary(a.LoanApplicationID, RiskSummary.Decide(assessing, high, medium + low),
                                           high, medium, low, a.Documents, a.InvestigatedAt);
                }, StringComparer.OrdinalIgnoreCase);
        }

        public async Task<(IReadOnlyList<StatementRow> Rows, IReadOnlyList<PageSummary> Pages, IReadOnlyList<DocField> Fields)>
            GetExtractedAsync(int loanApplicationId, CancellationToken ct = default)
        {
            var docIds = _context.CaseDocuments.Where(d => d.LoanApplicationID == loanApplicationId).Select(d => d.CaseDocumentID);

            var rows = await _context.StatementRows.AsNoTracking().Where(r => docIds.Contains(r.CaseDocumentID)).ToListAsync(ct);
            var pages = await _context.PageSummaries.AsNoTracking().Where(p => docIds.Contains(p.CaseDocumentID)).ToListAsync(ct);
            var fields = await _context.DocFields.AsNoTracking().Where(f => docIds.Contains(f.CaseDocumentID)).ToListAsync(ct);
            return (rows, pages, fields);
        }

        public async Task<IReadOnlyList<Finding>> GetFindingsAsync(int loanApplicationId, CancellationToken ct = default)
        {
            var findings = await _context.Findings.AsNoTracking()
                .Include(f => f.Evidence.OrderBy(e => e.EvidenceID))
                .Where(f => f.LoanApplicationID == loanApplicationId)
                .ToListAsync(ct);

            // Severity is stored as a name, so order in memory rather than alphabetically in SQL
            return findings.OrderBy(f => f.ReviewStatus != ReviewStatus.Open)
                           .ThenByDescending(f => f.Severity)
                           .ThenBy(f => f.FindingID)
                           .ToList();
        }

        public async Task SaveExtractionAsync(int caseDocumentId, ExtractedDocument extracted, string actor,
                                              CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            await _context.StatementRows.Where(r => r.CaseDocumentID == caseDocumentId).ExecuteDeleteAsync(ct);
            await _context.PageSummaries.Where(p => p.CaseDocumentID == caseDocumentId).ExecuteDeleteAsync(ct);
            await _context.DocFields.Where(f => f.CaseDocumentID == caseDocumentId).ExecuteDeleteAsync(ct);

            int rowNo = 0;
            foreach (var page in extracted.Pages.OrderBy(p => p.PageNo))
            {
                _context.PageSummaries.Add(new PageSummary
                {
                    CaseDocumentID = caseDocumentId, PageNo = page.PageNo,
                    OpeningBalance = page.OpeningBalance, ClosingBalance = page.ClosingBalance,
                    StatedTotalDebit = page.StatedTotalDebit, StatedTotalCredit = page.StatedTotalCredit
                });
                foreach (var r in page.Rows)
                {
                    _context.StatementRows.Add(new StatementRow
                    {
                        CaseDocumentID = caseDocumentId, PageNo = page.PageNo, RowNo = ++rowNo,
                        TxnDate = r.Date, Description = Clip(r.Description, 400)!, Debit = r.Debit, Credit = r.Credit,
                        Balance = r.Balance, CounterpartyName = Clip(r.CounterpartyName, 200),
                        CounterpartyAcct = Clip(r.CounterpartyAccount, 50), Box = Clip(r.Box, 40)
                    });
                }
            }
            foreach (var f in extracted.Fields)
            {
                _context.DocFields.Add(new DocField
                {
                    CaseDocumentID = caseDocumentId, Name = Clip(f.Name, 50)!, Value = Clip(f.Value, 400)!,
                    NumValue = f.NumValue, PageNo = f.PageNo, Box = Clip(f.Box, 40)
                });
            }

            var doc = await _context.CaseDocuments.FirstAsync(d => d.CaseDocumentID == caseDocumentId, ct);
            doc.DocType = extracted.DocType;
            doc.ClaimedIssuer = Clip(extracted.ClaimedIssuer, 200);
            doc.LayoutText = extracted.LayoutText;
            doc.ExtractStatus = ExtractStatus.Done;
            doc.ExtractError = null;
            doc.UpdatedBy = actor;

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        // Model output is untrusted input: fit it to the columns rather than fail the save
        private static string? Clip(string? s, int max) => s == null || s.Length <= max ? s : s[..max];

        public async Task ReplaceFindingsAsync(int loanApplicationId, FindingSource source, IReadOnlyList<Finding> fresh,
                                               CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var existing = await _context.Findings
                .Where(f => f.LoanApplicationID == loanApplicationId && f.DetectedBy == source)
                .ToListAsync(ct);
            var reviewedKeys = existing.Where(f => f.ReviewStatus != ReviewStatus.Open).Select(f => f.DedupKey).ToHashSet();

            // Deletes first, in their own save: the unique (application, key) index would
            // otherwise see the replacement before the row it replaces is gone.
            _context.Findings.RemoveRange(existing.Where(f => f.ReviewStatus == ReviewStatus.Open));
            await _context.SaveChangesAsync(ct);

            var toAdd = fresh.Where(f => !reviewedKeys.Contains(f.DedupKey)).ToList();
            _context.Findings.AddRange(toAdd);

            if (toAdd.Count > 0)
            {
                var application = await _context.LoanApplications.FirstAsync(a => a.LoanApplicationID == loanApplicationId, ct);
                if (application.Status == CaseStatus.New) application.Status = CaseStatus.Review;
            }

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task RecordInvestigationAsync(int loanApplicationId, DateTime at, string model, IReadOnlyList<AiDropLog> drops,
                                                   CancellationToken ct = default)
        {
            _context.AiDropLogs.AddRange(drops);
            await _context.SaveChangesAsync(ct);
            await _context.LoanApplications.Where(a => a.LoanApplicationID == loanApplicationId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.InvestigatedAt, at)
                                          .SetProperty(a => a.InvestigatedBy, model), ct);
        }

        public async Task<IReadOnlyList<AiDropLog>> GetLatestDropsAsync(int loanApplicationId, CancellationToken ct = default)
        {
            // The run's own timestamp, not the newest drop: a clean run must not show the last run's rejects
            var at = await _context.LoanApplications.Where(a => a.LoanApplicationID == loanApplicationId)
                                   .Select(a => a.InvestigatedAt).FirstOrDefaultAsync(ct);
            if (at == null) return Array.Empty<AiDropLog>();
            return await _context.AiDropLogs.AsNoTracking()
                                 .Where(d => d.LoanApplicationID == loanApplicationId && d.At == at)
                                 .OrderBy(d => d.AiDropLogID).ToListAsync(ct);
        }

        public Task SetStatusAsync(int loanApplicationId, CaseStatus status, CancellationToken ct = default)
            => _context.LoanApplications.Where(a => a.LoanApplicationID == loanApplicationId)
                   .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, status)
                                             .SetProperty(a => a.UpdatedAt, DateTime.UtcNow), ct);

        public Task MarkExtractionFailedAsync(int caseDocumentId, string error, CancellationToken ct = default)
            => _context.CaseDocuments.Where(d => d.CaseDocumentID == caseDocumentId)
                   .ExecuteUpdateAsync(s => s.SetProperty(d => d.ExtractStatus, ExtractStatus.Failed)
                                             .SetProperty(d => d.ExtractError, Clip(error, 2000))
                                             .SetProperty(d => d.UpdatedAt, DateTime.UtcNow), ct);

        public async Task<IReadOnlyList<int>> GetAwaitingExtractionAsync(CancellationToken ct = default)
            => await _context.CaseDocuments.Where(d => d.ExtractStatus == ExtractStatus.Pending)
                   .Select(d => d.LoanApplicationID).Distinct().ToListAsync(ct);

        public async Task<bool> ReviewFindingAsync(int loanApplicationId, int findingId, ReviewStatus status,
                                                   string reviewedBy, string? note, CancellationToken ct = default)
        {
            var finding = await _context.Findings
                .FirstOrDefaultAsync(f => f.FindingID == findingId && f.LoanApplicationID == loanApplicationId, ct);
            if (finding == null) return false;

            finding.ReviewStatus = status;
            finding.ReviewedBy = status == ReviewStatus.Open ? null : reviewedBy;
            finding.ReviewedAt = status == ReviewStatus.Open ? null : DateTime.UtcNow;
            finding.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            finding.UpdatedBy = reviewedBy;
            await _context.SaveChangesAsync(ct);
            return true;
        }
    }
}
