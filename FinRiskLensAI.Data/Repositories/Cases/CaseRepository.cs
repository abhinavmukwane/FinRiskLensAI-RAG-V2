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

        public async Task ReplaceDeterministicFindingsAsync(int loanApplicationId, IReadOnlyList<Finding> fresh,
                                                            CancellationToken ct = default)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(ct);

            var existing = await _context.Findings
                .Where(f => f.LoanApplicationID == loanApplicationId && f.DetectedBy == FindingSource.Deterministic)
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
