using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Interfaces.IServices.GST;
using FinRiskLensAI.Core.Models.GST;
using FinRiskLensAI.Core.Models.User_Activity;
using Microsoft.AspNetCore.Http;

namespace FinRiskLensAI.Services.Implementation.GST
{
    /// <summary>
    /// Reads the latest stored GSTR-2B/3B response for the logged-in MSME.
    /// Reuses the generic IRepository&lt;T&gt; (no dedicated repository) and the
    /// session's UserSessionModel for the UdyamNumber.
    /// </summary>
    public class GSTR2And3BResponceService : IGSTR2And3BResponceService
    {
        private readonly IRepository<GSTR2And3BResponceModel> _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GSTR2And3BResponceService(
            IRepository<GSTR2And3BResponceModel> repository,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
        }

        public Task<GSTR2And3BResponceResult?> GetResponces()
        {
            // UdyamNumber comes from the authenticated session, never from the caller.
            var udyamNumber = GetSessionUdyamNumber();
            return string.IsNullOrWhiteSpace(udyamNumber)
                ? Task.FromResult<GSTR2And3BResponceResult?>(null)
                : GetResponcesByUan(udyamNumber);
        }

        public async Task<GSTR2And3BResponceResult?> GetResponcesByUan(string udyamNumber)
        {
            if (string.IsNullOrWhiteSpace(udyamNumber))
                return null;

            udyamNumber = udyamNumber.Trim();
            var records = await _repository.FindAsync(x => x.UdyamNumber == udyamNumber);

            var latest = records
                .OrderByDescending(x => x.CreatedDate)
                .ThenByDescending(x => x.Id)
                .FirstOrDefault();

            if (latest == null)
                return null;

            return new GSTR2And3BResponceResult
            {
                GSTR2BResponseData = latest.GSTR2BResponseData,
                GSTR3BResponseData = latest.GSTR3BResponseData,
                GSTINNumber = latest.GSTINNumber,
                FilingPeriod = latest.FilingPeriod,
                CreatedDate = latest.CreatedDate
            };
        }

        /// <summary>
        /// The logged-in MSME's UdyamNumber from session. The SessionExtensions
        /// helper (GetCurrentUser) lives in the Web layer and can't be referenced
        /// here, so the same "CurrentUser" slot and UserSessionModel shape are read
        /// directly with the same System.Text.Json serializer.
        /// </summary>
        private string? GetSessionUdyamNumber()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var json = session?.GetString("CurrentUser");   // SessionKeys.CurrentUser
            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<UserSessionModel>(json)?.UdyamNumber;
        }
    }
}
