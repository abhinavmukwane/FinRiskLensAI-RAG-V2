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
        //  Score history (append-only series)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends one row. There is deliberately no upsert and no de-duplication:
        /// two runs on the same data are two real observations, and an audit trail
        /// that quietly drops one is not an audit trail.
        /// </summary>
        public async Task AddScoreHistoryAsync(MsmeScoreHistory history, CancellationToken ct = default)
        {
            if (history == null || string.IsNullOrWhiteSpace(history.Uan)) return;

            history.Uan = history.Uan.Trim();
            _context.MsmeScoreHistories.Add(history);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<ScoreHistoryPoint>> GetScoreHistoryAsync(
            string uan, int take = 50, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan)) return Array.Empty<ScoreHistoryPoint>();

            var key = uan.Trim();
            // Newest N by the index, then flipped to oldest-first for the chart's x-axis.
            var rows = await _context.MsmeScoreHistories
                .AsNoTracking()
                .Where(x => x.Uan == key)
                .OrderByDescending(x => x.ComputedAt)
                .ThenByDescending(x => x.MsmeScoreHistoryID)
                .Take(Math.Clamp(take, 1, 500))
                .Select(x => new ScoreHistoryPoint
                {
                    ComputedAt = x.ComputedAt,
                    OverallScore = x.OverallScore,
                    ScoreBand = x.ScoreBand,
                    RevenueVitality = x.RevenueVitality,
                    CashFlowHealth = x.CashFlowHealth,
                    TransactionTrust = x.TransactionTrust,
                    ComplianceQuotient = x.ComplianceQuotient,
                    BusinessStability = x.BusinessStability,
                    DebtServiceability = x.DebtServiceability,
                    TotalIndicativeEligibility = x.TotalIndicativeEligibility,
                    IsAnomalous = x.IsAnomalous
                })
                .ToListAsync(ct);

            rows.Reverse();
            return rows;
        }

        // ─────────────────────────────────────────────────────────────
        //  Portfolio queries
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Normalises a place / category name for grouping and display.
        /// <para>
        /// The Udyam feed returns the same value in different cases — "MAHARASHTRA"
        /// and "Maharashtra", "GUJARAT" and "Gujarat" — which would otherwise show as
        /// separate bars on the dashboard and separate options in the filter. Grouping
        /// on this normalised form collapses them into one.
        /// </para>
        /// </summary>
        private static string NormalizeName(string? value)
        {
            var s = (value ?? string.Empty).Trim();
            if (s.Length == 0) return s;

            // Collapse repeated internal whitespace, then Title Case from lower so
            // ALL-CAPS input is reduced rather than left as-is.
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
            return System.Globalization.CultureInfo.InvariantCulture.TextInfo
                .ToTitleCase(s.ToLowerInvariant());
        }

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

        /// <summary>Tidies the place names on a materialised row for display.</summary>
        private static void NormalizeRow(BankCustomerRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.State)) row.State = NormalizeName(row.State);
            if (!string.IsNullOrWhiteSpace(row.City)) row.City = NormalizeName(row.City);
        }

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
            {
                // The dropdown offers the normalised name, but rows hold whatever
                // casing the Udyam feed sent — compare lowered so both match. This
                // also keeps the filter correct on PostgreSQL, whose default
                // collation is case-sensitive unlike SQL Server's.
                var state = query.State.Trim().ToLower();
                q = q.Where(x => x.State != null && x.State.ToLower() == state);
            }

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
            rows.ForEach(NormalizeRow);

            return new BankCustomerPage { Rows = rows, TotalCount = total, Page = page, PageSize = size };
        }

        public async Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan)) return null;
            var key = uan.Trim();
            var row = await CustomerQuery().FirstOrDefaultAsync(x => x.Uan == key, ct);
            if (row != null) NormalizeRow(row);
            return row;
        }

        /// <summary>
        /// Distinct states for the filter dropdown, de-duplicated on the normalised
        /// name so the list shows "Maharashtra" once rather than both casings.
        /// </summary>
        public async Task<List<string>> GetStatesAsync(CancellationToken ct = default)
        {
            var raw = await _context.MsmeEnquiries.AsNoTracking()
                .Where(x => x.State != null && x.State != "")
                .Select(x => x.State!)
                .Distinct()
                .ToListAsync(ct);

            return raw.Select(NormalizeName)
                .Where(s => s.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

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

            // Grouped on the normalised name so mixed-case duplicates from the Udyam
            // feed ("MAHARASHTRA" / "Maharashtra") collapse into a single bar.
            stats.TopStates = all.Where(x => !string.IsNullOrWhiteSpace(x.State))
                .GroupBy(x => NormalizeName(x.State)).OrderByDescending(g => g.Count()).Take(8)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ActivityMix = all.Where(x => !string.IsNullOrWhiteSpace(x.MajorActivity))
                .GroupBy(x => NormalizeName(x.MajorActivity)).OrderByDescending(g => g.Count()).Take(6)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ProductMix = scored.Where(x => !string.IsNullOrWhiteSpace(x.TopProductName))
                .GroupBy(x => x.TopProductName!).OrderByDescending(g => g.Count()).Take(6)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.RecentCustomers = all.OrderByDescending(x => x.OnboardedOn).Take(8).ToList();

            return stats;
        }
    }
}
