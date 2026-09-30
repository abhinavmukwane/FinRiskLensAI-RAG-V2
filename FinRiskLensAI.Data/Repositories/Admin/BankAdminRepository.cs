using FinRiskLensAI.Core.Interfaces.IRepositories.Admin;
using FinRiskLensAI.Core.Models.Admin;
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
        /// Onboarded MSMEs joined to their Udyam profile. LEFT join — an MSME that
        /// has registered but not yet been matched to an enquiry must still appear.
        /// </summary>
        private IQueryable<BankCustomerRow> CustomerQuery()
            => from reg in _context.UserRegistration.AsNoTracking()
               join enq in _context.MsmeEnquiries.AsNoTracking()
                    on reg.MsmeEnquiryID equals enq.MsmeEnquiryID into enqJoin
               from enq in enqJoin.DefaultIfEmpty()
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
                   OnboardedOn = reg.CreatedAt
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

            if (!string.IsNullOrWhiteSpace(query.State))
            {
                // The dropdown offers the normalised name, but rows hold whatever
                // casing the Udyam feed sent — compare lowered so both match. This
                // also keeps the filter correct on PostgreSQL, whose default
                // collation is case-sensitive unlike SQL Server's.
                var state = query.State.Trim().ToLower();
                q = q.Where(x => x.State != null && x.State.ToLower() == state);
            }

            var total = await q.CountAsync(ct);

            q = (query.SortBy?.ToLowerInvariant()) switch
            {
                "name" => query.SortDesc ? q.OrderByDescending(x => x.EnterpriseName) : q.OrderBy(x => x.EnterpriseName),
                _ => query.SortDesc ? q.OrderByDescending(x => x.OnboardedOn) : q.OrderBy(x => x.OnboardedOn)
            };

            var page = Math.Max(1, query.Page);
            var size = Math.Clamp(query.PageSize, 5, 200);

            var rows = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
            rows.ForEach(NormalizeRow);
            await AttachLatestPushAsync(rows, ct);

            return new BankCustomerPage { Rows = rows, TotalCount = total, Page = page, PageSize = size };
        }

        public async Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan)) return null;
            var key = uan.Trim();
            var row = await CustomerQuery().FirstOrDefaultAsync(x => x.Uan == key, ct);
            if (row != null)
            {
                NormalizeRow(row);
                await AttachLatestPushAsync(new[] { row }, ct);
            }
            return row;
        }

        /// <summary>
        /// Stamps each row with its most recent loan-case push (any channel). One
        /// query for the whole page rather than a join in CustomerQuery, because the
        /// push table is append-only and "latest per UAN" is awkward to express in
        /// a translatable left join.
        /// </summary>
        private async Task AttachLatestPushAsync(IReadOnlyCollection<BankCustomerRow> rows, CancellationToken ct)
        {
            if (rows.Count == 0) return;
            var keys = rows.Select(r => r.Uan).Distinct().ToList();

            var latest = await _context.LoanCasePushes.AsNoTracking()
                .Where(x => keys.Contains(x.Uan))
                .OrderByDescending(x => x.PushedAt).ThenByDescending(x => x.LoanCasePushID)
                .Select(x => new
                {
                    x.Uan,
                    Summary = new Core.Models.LoanCase.LoanCasePushSummary
                    {
                        LoanCasePushID = x.LoanCasePushID,
                        Channel = x.Channel,
                        Status = x.Status,
                        CaseReference = x.CaseReference,
                        PushedAt = x.PushedAt,
                        PushedByName = x.PushedByName
                    }
                })
                .ToListAsync(ct);

            var byUan = latest.GroupBy(x => x.Uan).ToDictionary(g => g.Key, g => g.First().Summary);
            foreach (var row in rows)
                if (byUan.TryGetValue(row.Uan, out var s)) row.LastPush = s;
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
            var now = DateTime.UtcNow;

            var stats = new BankPortfolioStats
            {
                TotalOnboarded = all.Count,
                NewThisMonth = all.Count(x => x.OnboardedOn.Year == now.Year && x.OnboardedOn.Month == now.Month)
            };

            // Trailing 12 months of onboarding, including months with no signups
            var cursor = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
            for (var i = 0; i < 12; i++)
            {
                var key = cursor.ToString("yyyy-MM");
                stats.OnboardingTrend[key] = all.Count(x =>
                    x.OnboardedOn.Year == cursor.Year && x.OnboardedOn.Month == cursor.Month);
                cursor = cursor.AddMonths(1);
            }

            // Grouped on the normalised name so mixed-case duplicates from the Udyam
            // feed ("MAHARASHTRA" / "Maharashtra") collapse into a single bar.
            stats.TopStates = all.Where(x => !string.IsNullOrWhiteSpace(x.State))
                .GroupBy(x => NormalizeName(x.State)).OrderByDescending(g => g.Count()).Take(8)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.ActivityMix = all.Where(x => !string.IsNullOrWhiteSpace(x.MajorActivity))
                .GroupBy(x => NormalizeName(x.MajorActivity)).OrderByDescending(g => g.Count()).Take(6)
                .ToDictionary(g => g.Key, g => g.Count());

            stats.RecentCustomers = all.OrderByDescending(x => x.OnboardedOn).Take(8).ToList();

            return stats;
        }
    }
}
