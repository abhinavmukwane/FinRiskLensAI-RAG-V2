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
    }
}
