using FinRiskLensAI.Core.Interfaces.IRepositories.Admin;
using FinRiskLensAI.Core.Models.LoanCase;
using FinRiskLensAI.Data.DbContextEDMX;
using Microsoft.EntityFrameworkCore;

namespace FinRiskLensAI.Data.Repositories.Admin
{
    public class LoanCasePushRepository : ILoanCasePushRepository
    {
        private readonly ApplicationDbContext _context;

        public LoanCasePushRepository(ApplicationDbContext context) => _context = context;

        public async Task<int> AddAsync(LoanCasePush push, CancellationToken ct = default)
        {
            push.Uan = push.Uan.Trim();
            _context.LoanCasePushes.Add(push);
            await _context.SaveChangesAsync(ct);
            return push.LoanCasePushID;
        }

        public Task<LoanCasePush?> GetAsync(int loanCasePushId, CancellationToken ct = default)
            => _context.LoanCasePushes.AsNoTracking()
                   .FirstOrDefaultAsync(x => x.LoanCasePushID == loanCasePushId, ct);

        public async Task<IReadOnlyList<LoanCasePushSummary>> GetLatestByChannelAsync(string uan, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan)) return Array.Empty<LoanCasePushSummary>();
            var key = uan.Trim();

            // Three channels, a handful of rows per UAN at most — pull them and pick
            // the newest per channel in memory rather than a windowed SQL query.
            var rows = await _context.LoanCasePushes.AsNoTracking()
                .Where(x => x.Uan == key)
                .OrderByDescending(x => x.PushedAt).ThenByDescending(x => x.LoanCasePushID)
                .Select(Projection)
                .ToListAsync(ct);

            return rows.GroupBy(x => x.Channel).Select(g => g.First()).OrderBy(x => x.Channel).ToList();
        }

        public async Task<IReadOnlyDictionary<string, LoanCasePushSummary>> GetLatestForAsync(
            IEnumerable<string> uans, CancellationToken ct = default)
        {
            var keys = uans.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
            if (keys.Count == 0) return new Dictionary<string, LoanCasePushSummary>();

            // Narrow projection with the UAN alongside, so the whole entity (and its
            // nvarchar(max) payload) is never pulled just to draw a badge.
            var rows = await _context.LoanCasePushes.AsNoTracking()
                .Where(x => keys.Contains(x.Uan))
                .OrderByDescending(x => x.PushedAt).ThenByDescending(x => x.LoanCasePushID)
                .Select(x => new
                {
                    x.Uan,
                    Summary = new LoanCasePushSummary
                    {
                        LoanCasePushID = x.LoanCasePushID,
                        Channel = x.Channel,
                        Status = x.Status,
                        CaseReference = x.CaseReference,
                        ScoreAtPush = x.ScoreAtPush,
                        BandAtPush = x.BandAtPush,
                        PushedAt = x.PushedAt,
                        PushedByName = x.PushedByName
                    }
                })
                .ToListAsync(ct);

            return rows.GroupBy(x => x.Uan).ToDictionary(g => g.Key, g => g.First().Summary);
        }

        private static readonly System.Linq.Expressions.Expression<Func<LoanCasePush, LoanCasePushSummary>> Projection =
            x => new LoanCasePushSummary
            {
                LoanCasePushID = x.LoanCasePushID,
                Channel = x.Channel,
                Status = x.Status,
                CaseReference = x.CaseReference,
                ScoreAtPush = x.ScoreAtPush,
                BandAtPush = x.BandAtPush,
                PushedAt = x.PushedAt,
                PushedByName = x.PushedByName
            };
    }
}
