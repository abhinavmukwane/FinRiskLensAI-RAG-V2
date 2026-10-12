using System.ComponentModel.DataAnnotations;

namespace FinRiskLensAI.Core.Models.Cases
{
    /// <summary>The new-case form. Amounts are monthly.</summary>
    public class NewCaseRequest
    {
        [Required, StringLength(50)]
        [Display(Name = "Application number")]
        public string ApplicantRef { get; set; } = string.Empty;

        public CaseProduct ProductType { get; set; } = CaseProduct.SmeLoan;

        // Character classes, not (?i): the same pattern is emitted for client-side validation
        [RegularExpression(@"^\s*[Uu][Dd][Yy][Aa][Mm]-[A-Za-z]{2}-\d{2}-\d{7}\s*$", ErrorMessage = "Expected UDYAM-XX-00-0000000.")]
        [Display(Name = "Udyam number")]
        public string? Uan { get; set; }

        [Range(0, 999_999_999_999)]
        public decimal? DeclaredIncome { get; set; }

        [StringLength(200)]
        public string? DeclaredEmployer { get; set; }

        [Range(0, 999_999_999_999)]
        public decimal? DeclaredEmis { get; set; }
    }

    public sealed record CaseUpload(string FileName, byte[] Content);

    public sealed record CaseIntakeResult(int? LoanApplicationID, string? Error);

    public sealed record CaseListItem(int LoanApplicationID, string ApplicantRef, CaseProduct ProductType,
                                      string? Uan, CaseStatus Status, DateTime SubmittedAt, int DocumentCount);

    public class CaseDetailViewModel
    {
        public LoanApplication Application { get; set; } = new();
        /// <summary>Document id → other applications holding a byte-identical file.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<int>> SharedWith { get; set; } = new Dictionary<int, IReadOnlyList<int>>();
        /// <summary>Most severe first; reviewed findings after open ones.</summary>
        public IReadOnlyList<Finding> Findings { get; set; } = Array.Empty<Finding>();
        /// <summary>AI claims the grounding verifier rejected in the latest investigation — shown, not hidden.</summary>
        public IReadOnlyList<AiDropLog> RejectedClaims { get; set; } = Array.Empty<AiDropLog>();
    }

    public sealed record CaseFile(Stream Content, string ContentType, string FileName);
}
