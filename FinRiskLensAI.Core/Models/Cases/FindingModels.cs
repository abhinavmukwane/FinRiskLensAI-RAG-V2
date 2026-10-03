using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Cases
{
    // Findings, not scores — Doc/10_FRAUD_LENS_BUILD.md §4. Every finding names what
    // was wrong and points at the rows, fields or pages that show it.

    public enum FindingCategory { Tampering, IncomeInflation, HiddenLiabilities, CrossDocument, Behavioural, Fabrication, Network }

    public enum FindingSeverity { Low, Medium, High }

    public enum FindingSource { Deterministic, AiVerified }

    public enum ReviewStatus { Open, Confirmed, Dismissed }

    public enum EvidenceKind { Row, Field, Page, Doc, App }

    public class Finding : AuditableEntity
    {
        public int FindingID { get; set; }
        public int LoanApplicationID { get; set; }

        /// <summary>Catalogue id, e.g. BALANCE_DISCONTINUITY.</summary>
        public string Type { get; set; } = string.Empty;
        public FindingCategory Category { get; set; }
        public FindingSeverity Severity { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
        public string? RecommendedAction { get; set; }
        public FindingSource DetectedBy { get; set; }

        /// <summary>
        /// Identity of the finding across re-runs (type + what it is about), so a re-run
        /// neither duplicates a finding nor resurrects one an investigator dismissed.
        /// </summary>
        public string DedupKey { get; set; } = string.Empty;

        public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Open;
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNote { get; set; }

        public List<Evidence> Evidence { get; set; } = new();
    }

    public class Evidence
    {
        public long EvidenceID { get; set; }
        public int FindingID { get; set; }
        public EvidenceKind Kind { get; set; }
        /// <summary>StatementRowID / DocFieldID / CaseDocumentID / LoanApplicationID, by kind.</summary>
        public long? RefId { get; set; }
        /// <summary>The document the evidence sits in — for the viewer link.</summary>
        public int? CaseDocumentID { get; set; }
        public int? PageNo { get; set; }
        /// <summary>"ymin,xmin,ymax,xmax", 0–1000, copied from the row or field.</summary>
        public string? Box { get; set; }
        public string? Note { get; set; }
    }
}
