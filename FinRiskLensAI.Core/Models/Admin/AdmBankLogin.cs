using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Admin
{
    /// <summary>
    /// Bank-side portal user (ADM_BankLogin). Separate from ADM_Login because a
    /// bank user carries branch identity — IFSC, branch code/name and address —
    /// that a platform admin does not. Login is user id + password (no OTP).
    /// </summary>
    public class AdmBankLogin : AuditableEntity
    {
        public int AdmBankLoginID { get; set; }

        // ── Login credentials
        /// <summary>Login user id, e.g. an employee code. Unique.</summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>ASP.NET Identity V3 PBKDF2 hash — never store plaintext.</summary>
        public string PasswordHash { get; set; } = string.Empty;

        // ── Person
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? MobileNumber { get; set; }
        public string? Designation { get; set; }

        // ── Bank / branch identity
        /// <summary>Branch IFSC — 11 chars, 4 alpha bank code + '0' + 6 branch code.</summary>
        public string IfscCode { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? BranchCode { get; set; }
        public string? BranchName { get; set; }
        public string? BranchAddress { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Pin { get; set; }

        // ── Access control
        /// <summary>BankAdmin / CreditOfficer / Viewer.</summary>
        public string Role { get; set; } = "BankAdmin";
        public bool IsActive { get; set; } = true;

        public DateTime? LastLoginAt { get; set; }
        public int FailedLoginCount { get; set; }
    }
}
