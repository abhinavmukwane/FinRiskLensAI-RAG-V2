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

    /// <summary>What the network lens reads of one application: who it names, its documents' fingerprints, its salary credits.</summary>
    public sealed record NetworkApplication(int LoanApplicationID, string ApplicantRef, string? DeclaredEmployer,
                                            IReadOnlyList<CaseDocument> Documents, IReadOnlyList<StatementRow> SalaryCredits,
                                            string? Uan = null);

    /// <summary>A customer's overall state on the bank side (Doc/12_CUSTOMER_RISK_LENS.md).</summary>
    public enum RiskVerdict { AwaitingData, Assessing, Clear, Review, HighRisk }

    /// <summary>One customer's assessment at a glance: the verdict and what drives it.</summary>
    public sealed record RiskSummary(int LoanApplicationID, RiskVerdict Verdict, int OpenHigh, int OpenMedium, int OpenLow,
                                     int Documents, DateTime? InvestigatedAt)
    {
        /// <summary>
        /// The one rule for the verdict. Still reading or investigating → Assessing; any open High finding →
        /// High risk; any other open finding → Review; otherwise Clear. Reviewed findings no longer count.
        /// </summary>
        public static RiskVerdict Decide(bool assessing, int openHigh, int openOther)
            => assessing ? RiskVerdict.Assessing
             : openHigh > 0 ? RiskVerdict.HighRisk
             : openOther > 0 ? RiskVerdict.Review
             : RiskVerdict.Clear;

        public static string Label(RiskVerdict v) => v switch
        {
            RiskVerdict.AwaitingData => "Awaiting data",
            RiskVerdict.Assessing => "Assessing",
            RiskVerdict.Clear => "Clear",
            RiskVerdict.Review => "Review",
            _ => "High risk"
        };
    }

    /// <summary>The Risk Lens sections (Doc/12_CUSTOMER_RISK_LENS.md §4), and which section each finding type belongs to.</summary>
    public static class RiskSections
    {
        public const string Identity = "identity", Gst = "gst", Bank = "bank", Itr = "itr", Documents = "documents", Network = "network";

        public static string Of(string findingType) => findingType switch
        {
            "IDENTITY_MISMATCH" or "IMPLAUSIBLE_EMPLOYER" or "EMPLOYER_INACTIVE" => Identity,
            "INCOME_MISMATCH" => Gst,
            "ITR_MISMATCH" => Itr,
            "SALARY_ROUNDTRIP" or "PRE_APPLICATION_WINDOW_DRESSING" or "PASS_THROUGH" or "BOUNCES" or "UNDECLARED_EMI"
                or "LOAN_STACKING" or "GAMBLING_CRYPTO_EXPOSURE" or "SALARY_SOURCE_MISMATCH" => Bank,
            "FRAUD_RING" => Network,
            _ => Documents   // tampering, file history, the bank-record comparison
        };
    }

    /// <summary>One finding card, wherever it is shown — the case page or a customer's Risk Lens.</summary>
    public sealed record FindingCardModel(Finding Finding, IReadOnlyDictionary<int, CaseDocument> Documents,
                                          int LoanApplicationID, string? ReturnUrl);

    /// <summary>One thing separate applications share — a file, a template, a salary account — and each one's evidence of it.</summary>
    public sealed record RingLink(string Kind, string Detail, IReadOnlyDictionary<int, IReadOnlyList<Evidence>> Members);

    /// <summary>Applications joined, directly or through each other, by at least one link.</summary>
    public sealed record Ring(IReadOnlyList<NetworkApplication> Members, IReadOnlyList<RingLink> Links);
}
