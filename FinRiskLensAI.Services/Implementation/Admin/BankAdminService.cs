using FinRiskLensAI.Core.Common;
using FinRiskLensAI.Core.Interfaces.IRepositories.Admin;
using FinRiskLensAI.Core.Interfaces.IServices.Admin;
using FinRiskLensAI.Core.Models;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Scoring;
using Microsoft.AspNetCore.Identity;

namespace FinRiskLensAI.Services.Implementation.Admin
{
    /// <summary>
    /// Bank-portal authentication and the SQL score-summary projection.
    /// Passwords are hashed with ASP.NET Identity's PasswordHasher (PBKDF2,
    /// Identity V3 format) — never hand-rolled, never stored in plaintext.
    /// </summary>
    public class BankAdminService : IBankAdminService
    {
        private readonly IBankAdminRepository _repo;
        private readonly PasswordHasher<AdmBankLogin> _hasher = new();

        public BankAdminService(IBankAdminRepository repo) => _repo = repo;

        public async Task<ResultModel<BankUserSessionModel>> ValidateLoginAsync(
            string userId, string password, CancellationToken ct = default)
        {
            var result = new ResultModel<BankUserSessionModel>();

            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
            {
                result.Result = tflResultType.tflUserNameOrPasswordBad;
                result.Message = "Please enter both User ID and Password.";
                return result;
            }

            try
            {
                var user = await _repo.GetByUserIdAsync(userId, ct);

                // Same message whether the id is unknown or the password is wrong —
                // don't let the response tell an attacker which user ids exist.
                if (user == null)
                {
                    result.Result = tflResultType.tflUserNameOrPasswordBad;
                    result.Message = "Invalid User ID or Password.";
                    return result;
                }

                if (!user.IsActive)
                {
                    result.Result = tflResultType.tflUserIsInActive;
                    result.Message = "This account is inactive. Please contact your administrator.";
                    return result;
                }

                var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
                if (verify == PasswordVerificationResult.Failed)
                {
                    await _repo.RecordLoginFailureAsync(user.AdmBankLoginID, ct);
                    result.Result = tflResultType.tflUserNameOrPasswordBad;
                    result.Message = "Invalid User ID or Password.";
                    return result;
                }

                await _repo.RecordLoginSuccessAsync(user.AdmBankLoginID, ct);

                result.Result = tflResultType.tflUserAuthenticated;
                result.Message = "Login successful.";
                result.Data = new BankUserSessionModel
                {
                    AdmBankLoginID = user.AdmBankLoginID,
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    Designation = user.Designation,
                    IfscCode = user.IfscCode,
                    BankName = user.BankName,
                    BranchName = user.BranchName,
                    BranchCode = user.BranchCode,
                    City = user.City,
                    State = user.State,
                    Role = user.Role
                };
                return result;
            }
            catch (Exception ex)
            {
                result.Result = tflResultType.tflError;
                result.Message = ex.Message;
                return result;
            }
        }

        public string HashPassword(string password)
            => _hasher.HashPassword(new AdmBankLogin(), password);

        public async Task SaveScoreSummaryAsync(string uan, RiskAnalysisResult result, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(uan) || result == null) return;

            var lending = result.Lending;
            var topProduct = result.Recommendations?.FirstOrDefault();

            await _repo.UpsertScoreSummaryAsync(new MsmeScoreSummary
            {
                Uan = uan.Trim(),
                OverallScore = result.OverallScore,
                ScoreBand = result.ScoreBand.ToString(),
                HeuristicScore = result.HeuristicScore,
                MlCalibratedScore = result.MlCalibratedScore,
                CashflowTrendSlope = result.CashflowTrendSlope,
                WorkingCapitalLimit = Money(lending?.WorkingCapitalLimit),
                TermLoanCapacity = Money(lending?.TermLoanCapacity),
                TotalIndicativeEligibility = Money(lending?.TotalIndicativeEligibility),
                AnnualTurnover = Money(lending?.AnnualTurnover),
                MonthlySurplus = Money(lending?.MonthlySurplus),
                TopProductName = topProduct?.ProductName,
                TopProductScheme = topProduct?.SchemeCode,
                IsAnomalous = result.Anomaly?.IsAnomalous ?? false,
                DimensionsExcludedCount = result.ExcludedDimensions?.Count ?? 0,
                ModelVersion = result.ModelVersion,
                ComputedAt = result.ComputedAt,
                UpdatedBy = "analysis"
            }, ct);
        }

        // ── Portfolio reads pass straight through to the repository; there is no
        //    extra business rule to apply, so no logic is duplicated here.
        public Task<BankCustomerPage> GetCustomersAsync(BankCustomerQuery query, CancellationToken ct = default)
            => _repo.GetCustomersAsync(query, ct);

        public Task<BankCustomerRow?> GetCustomerAsync(string uan, CancellationToken ct = default)
            => _repo.GetCustomerAsync(uan, ct);

        public Task<BankPortfolioStats> GetPortfolioStatsAsync(CancellationToken ct = default)
            => _repo.GetPortfolioStatsAsync(ct);

        public Task<List<string>> GetStatesAsync(CancellationToken ct = default)
            => _repo.GetStatesAsync(ct);

        /// <summary>
        /// double → decimal for the money columns. Guards NaN/Infinity and values
        /// outside decimal's range, which would otherwise throw on SaveChanges.
        /// </summary>
        private static decimal Money(double? value)
        {
            if (value is null) return 0m;
            var v = value.Value;
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0m;
            if (v <= (double)decimal.MinValue || v >= (double)decimal.MaxValue) return 0m;
            return Math.Round((decimal)v, 2);
        }
    }
}
