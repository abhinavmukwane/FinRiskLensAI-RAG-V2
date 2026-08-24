namespace FinRiskLensAI.Core.Models.Admin
{
    /// <summary>
    /// Snapshot of the logged-in bank user held in the HTTP session, so pages can
    /// read identity and branch without another DB hit. Plain DTO — not an EF
    /// entity, maps to no table, and deliberately carries no password material.
    /// </summary>
    public class BankUserSessionModel
    {
        public int AdmBankLoginID { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Designation { get; set; }

        public string IfscCode { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? BranchName { get; set; }
        public string? BranchCode { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }

        public string Role { get; set; } = "BankAdmin";
        public string? ClientIP { get; set; }

        /// <summary>Initials for the topbar avatar, e.g. "AM".</summary>
        public string Initials
        {
            get
            {
                var parts = (FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "?";
                return parts.Length == 1
                    ? parts[0][..1].ToUpperInvariant()
                    : string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
            }
        }
    }
}
