using FinRiskLensAI.Core.Interfaces.IRepositories.Admin;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Data.DbContextEDMX;
using Microsoft.EntityFrameworkCore;

namespace FinRiskLensAI.Data.Repositories.Admin
{
    public class BankAdminRepository : IBankAdminRepository
    {
        private readonly ApplicationDbContext _context;

        public BankAdminRepository(ApplicationDbContext context) => _context = context;

        public async Task<AdmBankLogin?> GetByUserIdAsync(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            var id = userId.Trim();
            return await _context.AdmBankLogins
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == id, ct);
        }

        public async Task RecordLoginSuccessAsync(int admBankLoginId, CancellationToken ct = default)
        {
            var user = await _context.AdmBankLogins.FirstOrDefaultAsync(x => x.AdmBankLoginID == admBankLoginId, ct);
            if (user == null) return;

            user.LastLoginAt = DateTime.UtcNow;
            user.FailedLoginCount = 0;
            await _context.SaveChangesAsync(ct);
        }

        public async Task RecordLoginFailureAsync(int admBankLoginId, CancellationToken ct = default)
        {
            var user = await _context.AdmBankLogins.FirstOrDefaultAsync(x => x.AdmBankLoginID == admBankLoginId, ct);
            if (user == null) return;

            user.FailedLoginCount += 1;
            await _context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// One row per UAN: update in place when present, insert otherwise. Called
        /// from the analysis pipeline every time a score is (re)computed.
        /// </summary>
        public async Task UpsertScoreSummaryAsync(MsmeScoreSummary summary, CancellationToken ct = default)
        {
            if (summary == null || string.IsNullOrWhiteSpace(summary.Uan)) return;

            var uan = summary.Uan.Trim();
            var existing = await _context.MsmeScoreSummaries.FirstOrDefaultAsync(x => x.Uan == uan, ct);

            if (existing == null)
            {
                summary.Uan = uan;
                _context.MsmeScoreSummaries.Add(summary);
            }
            else
            {
                existing.OverallScore = summary.OverallScore;
                existing.ScoreBand = summary.ScoreBand;
                existing.HeuristicScore = summary.HeuristicScore;
                existing.MlCalibratedScore = summary.MlCalibratedScore;
                existing.CashflowTrendSlope = summary.CashflowTrendSlope;
                existing.WorkingCapitalLimit = summary.WorkingCapitalLimit;
                existing.TermLoanCapacity = summary.TermLoanCapacity;
                existing.TotalIndicativeEligibility = summary.TotalIndicativeEligibility;
                existing.AnnualTurnover = summary.AnnualTurnover;
                existing.MonthlySurplus = summary.MonthlySurplus;
                existing.TopProductName = summary.TopProductName;
                existing.TopProductScheme = summary.TopProductScheme;
                existing.IsAnomalous = summary.IsAnomalous;
                existing.DimensionsExcludedCount = summary.DimensionsExcludedCount;
                existing.ModelVersion = summary.ModelVersion;
                existing.ComputedAt = summary.ComputedAt;
                existing.UpdatedBy = summary.UpdatedBy ?? "analysis";
            }

            await _context.SaveChangesAsync(ct);
        }

        // ─────────────────────────────────────────────────────────────
        //  Portfolio queries
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Onboarded MSMEs joined to their Udyam profile and score row. The score
        /// join is a LEFT join — an MSME that has registered but not yet been
        /// analysed must still appear in the list, flagged as pending.
        /// </summary>
        private IQueryable<BankCustomerRow> CustomerQuery()
            => from reg in _context.UserRegistration.AsNoTracking()
               join enq in _context.MsmeEnquiries.AsNoTracking()
                    on reg.MsmeEnquiryID equals enq.MsmeEnquiryID into enqJoin
               from enq in enqJoin.DefaultIfEmpty()
               join sum in _context.MsmeScoreSummaries.AsNoTracking()
                    on reg.UdyamNumber equals sum.Uan into sumJoin
               from sum in sumJoin.DefaultIfEmpty()
               where reg.UdyamNumber != null && reg.UdyamNumber != ""
               select new BankCustomerRow
               {
                   UserRegistrationID = reg.UserRegistrationID,
                   MsmeEnquiryID = reg.MsmeEnquiryID,
                   Uan = reg.UdyamNumber!,
                   EnterpriseName = enq != null ? enq.NameOfEnterprise : reg.UdyamNumber!,
                   GstinNumber = reg.GstinNumber ?? (enq != null ? enq.GstinNumber : null),
                   PanNumber = reg.PanNumber ?? (enq != null ? enq.PanNumber : null),
                   Email = reg.Email,
                   MobileNumber = reg.MobileNumber,
                   State = enq != null ? enq.State : null,
                   City = enq != null ? enq.City : null,
                   OrganizationType = enq != null ? enq.OrganizationType : null,
                   MajorActivity = enq != null ? enq.MajorActivity : null,
                   DateOfIncorporation = enq != null ? enq.DateOfIncorporation : null,
                   IPAddress = reg.IPAddress,
                   OnboardedOn = reg.CreatedAt,
                   OverallScore = sum != null ? sum.OverallScore : (double?)null,
                   ScoreBand = sum != null ? sum.ScoreBand : null,
                   TotalIndicativeEligibility = sum != null ? sum.TotalIndicativeEligibility : (decimal?)null,
                   TopProductName = sum != null ? sum.TopProductName : null,
                   IsAnomalous = sum != null && sum.IsAnomalous,
                   ComputedAt = sum != null ? sum.ComputedAt : (DateTime?)null
               };

        public async Task<BankCustomerPage> GetCustomersAsync(BankCustomerQuery query, CancellationToken ct = default)
        {
            query ??= new BankCustomerQuery();
            var q = CustomerQuery();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                q = q.Where(x => x.EnterpriseName.Contains(s)
                              || x.Uan.Contains(s)
                              || (x.GstinNumber != null && x.GstinNumber.Contains(s))
                              || (x.PanNumber != null && x.PanNumber.Contains(s))
                              || (x.Email != null && x.Email.Contains(s))
                              || (x.MobileNumber != null && x.MobileNumber.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(query.Band))
                q = q.Where(x => x.ScoreBand == query.Band);

            if (!string.IsNullOrWhiteSpace(query.State))
                q = q.Where(x => x.State == query.State);

            if (string.Equals(query.Status, "scored", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.OverallScore != null);
            else if (string.Equals(query.Status, "pending", StringComparison.OrdinalIgnoreCase))
                q = q.Where(x => x.OverallScore == null);

            var total = await q.CountAsync(ct);

            q = (query.SortBy?.ToLowerInvariant()) switch
            {
                "score" => query.SortDesc ? q.OrderByDescending(x => x.OverallScore) : q.OrderBy(x => x.OverallScore),
                "name" => query.SortDesc ? q.OrderByDescending(x => x.EnterpriseName) : q.OrderBy(x => x.EnterpriseName),
                "eligibility" => query.SortDesc ? q.OrderByDescending(x => x.TotalIndicativeEligibility) : q.OrderBy(x => x.TotalIndicativeEligibility),
                _ => query.SortDesc ? q.OrderByDescending(x => x.OnboardedOn) : q.OrderBy(x => x.OnboardedOn)
            };

            var page = Math.Max(1, query.Page);
            var size = Math.Clamp(query.PageSize, 5, 200);

            var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

            return new BankCustomerPage { Rows = rows, TotalCount = total, Page = page, PageSize = size };
        }

        public async Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan)) return null;
            var key = uan.Trim();
            return await CustomerQuery().FirstOrDefaultAsync(x => x.Uan == key, ct);
        }

        public async Task<List<string>> GetStatesAsync(CancellationToken ct = default)
            => await _context.MsmeEnquiries.AsNoTracking()
                .Where(x => x.State != null && x.State != "")
                .Select(x => x.State!)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync(ct);

        public async Task<BankPortfolioStats> GetPortfolioStatsAsync(CancellationToken ct = default)
        {
            // One materialisation feeds every tile and chart. The portfolio is
            // hundreds of rows, not millions — aggregating in memory here is far
            // simpler than a dozen round trips, and the projection is narrow.
            var all = await CustomerQuery().ToListAsync(ct);
            var scored = all.Where(x => x.OverallScore.HasValue).ToList();

            var now = DateTime.UtcNow;

            var stats = new BankPortfolioStats
            {
                TotalOnboarded = all.Count,
                TotalScored = scored.Count,
                LendableCount = scored.Count(x => x.ScoreBand == "Excellent" || x.ScoreBand == "Good"),
                NewThisMonth = all.Count(x => x.OnboardedOn.Year == now.Year && x.OnboardedOn.Month == now.Month),
                HighRiskCount = scored.Count(x => x.ScoreBand == "HighRisk" || x.ScoreBand == "AtRisk"),
                AnomalyCount = scored.Count(x => x.IsAnomalous)
            };

            // Fixed band order so the doughnut colours stay stable even when a band is empty
            foreach (var band in new[] { "Excellent", "Good", "Fair", "AtRisk", "HighRisk" })
                stats.BandDistribution[band] = scored.Count(x => x.ScoreBand == band);

            // Trailing 12 months of onboarding, including months with no signups
            var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
            for (var i = 0; i < 12; i++)
            {
                var key = cursor.ToString("yyyy-MM");
                stats.OnboardingTrend[key] = all.Count(x =>
                    x.OnboardedOn.Year == cursor.Year && x.OnboardedOn.Month == cursor.Month);
                cursor = cursor.AddMonths(1);
            }

            foreach (var bucket in new[] { "0-200", "201-400", "401-600", "601-800", "801-1000" })
                stats.ScoreHistogram[bucket] = 0;
            foreach (var s in scored)
            {
                var v = s.OverallScore!.Value;
                var bucket = v <= 200 ? "0-200" : v <= 400 ? "201-400" : v <= 600 ? "401-600" : v <= 800 ? "601-800" : "801-1000";
                stats.ScoreHistogram[bucket]++;
            }

            stats.TopStates = all.Where(x => !string.IsNullOrWhiteSpace(x.State))
                .GroupBy(x => x.State!).OrderByDescending(g => g.Count()).Take(8)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ActivityMix = all.Where(x => !string.IsNullOrWhiteSpace(x.MajorActivity))
                .GroupBy(x => x.MajorActivity!).OrderByDescending(g => g.Count()).Take(6)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ProductMix = scored.Where(x => !string.IsNullOrWhiteSpace(x.TopProductName))
                .GroupBy(x => x.TopProductName!).OrderByDescending(g => g.Count()).Take(6)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.RecentCustomers = all.OrderByDescending(x => x.OnboardedOn).Take(8).ToList();

            return stats;
        }
    }
}
