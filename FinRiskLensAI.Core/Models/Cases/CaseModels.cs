using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.Cases
{
    // The case side of FraudLens — Doc/10_FRAUD_LENS_BUILD.md §4. Uploaded documents
    // are the primary evidence; the registry and AA sources (keyed on the Udyam number)
    // are what they get checked against. Enums are stored as names, never ints.

    public enum CaseStatus { New, Processing, Review, Clear, Hold }

    public enum CaseProduct { SmeLoan, PersonalLoan, Card }

    public static class CaseLabels
    {
        public static string Label(this CaseProduct p) => p switch
        {
            CaseProduct.SmeLoan => "SME loan",
            CaseProduct.PersonalLoan => "Personal loan",
            _ => "Card"
        };
    }

    /// <summary>Set by the extractor when it classifies the file; an upload starts Unclassified.</summary>
    public enum CaseDocType { Unclassified, BankStatement, SalarySlip, Itr, GstReturn, KycId, UtilityBill, Invoice }

    public enum ExtractStatus { Pending, Done, Failed }

    /// <summary>One loan application under investigation.</summary>
    public class LoanApplication : AuditableEntity
    {
        public int LoanApplicationID { get; set; }
        /// <summary>The lender's own application number — pseudonymous, never a name.</summary>
        public string ApplicantRef { get; set; } = string.Empty;
        public CaseProduct ProductType { get; set; }
        /// <summary>Udyam number for SME cases — the key the verification sources hang off.</summary>
        public string? Uan { get; set; }
        /// <summary>Monthly, as declared on the application form.</summary>
        public decimal? DeclaredIncome { get; set; }
        public string? DeclaredEmployer { get; set; }
        /// <summary>Monthly EMIs the applicant declared — the baseline for UNDECLARED_EMI.</summary>
        public decimal? DeclaredEmis { get; set; }
        public DateTime SubmittedAt { get; set; }
        public CaseStatus Status { get; set; } = CaseStatus.New;
        /// <summary>Last Investigator run; its rejected claims carry the same timestamp.</summary>
        public DateTime? InvestigatedAt { get; set; }
        public string? InvestigatedBy { get; set; }
        /// <summary>When the assessment last read the bank's own record (Account Aggregator); null when it had none.</summary>
        public DateTime? BankDataAt { get; set; }

        public List<CaseDocument> Documents { get; set; } = new();
    }

    /// <summary>
    /// One uploaded file. The bytes live in the document store under a content-addressed
    /// path, so the same file reused across applications is stored once and its
    /// <see cref="Sha256"/> links the cases.
    /// </summary>
    public class CaseDocument : AuditableEntity
    {
        public int CaseDocumentID { get; set; }
        public int LoanApplicationID { get; set; }
        public LoanApplication? LoanApplication { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public byte[] Sha256 { get; set; } = Array.Empty<byte>();
        public CaseDocType DocType { get; set; } = CaseDocType.Unclassified;

        // PDF metadata (PdfPig) — null for images, and for PDFs PdfPig cannot open
        // (password-protected bank e-statements). METADATA_ANOMALY reads these.
        public int? PageCount { get; set; }
        public string? PdfProducer { get; set; }
        public string? PdfCreator { get; set; }
        public DateTime? PdfCreatedAt { get; set; }
        public DateTime? PdfModifiedAt { get; set; }
        /// <summary>Distinct font families, comma-separated, subset prefixes stripped.</summary>
        public string? FontSet { get; set; }

        // Extractor output
        /// <summary>The bank, employer or authority the document claims to come from.</summary>
        public string? ClaimedIssuer { get; set; }
        /// <summary>Layout description with no personal data — the template fingerprint source.</summary>
        public string? LayoutText { get; set; }
        public ExtractStatus ExtractStatus { get; set; } = ExtractStatus.Pending;
        public string? ExtractError { get; set; }
    }

    /// <summary>A bank-statement transaction, numbers exactly as printed — tampered maths is what we catch.</summary>
    public class StatementRow
    {
        public long StatementRowID { get; set; }
        public int CaseDocumentID { get; set; }
        public int PageNo { get; set; }
        /// <summary>Order within the document.</summary>
        public int RowNo { get; set; }
        public DateOnly TxnDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
        public decimal? Balance { get; set; }
        public string? CounterpartyName { get; set; }
        public string? CounterpartyAcct { get; set; }
        /// <summary>EMPLOYER, LENDER, BETTING, CRYPTO, FAMILY, MERCHANT, SELF, UNKNOWN.</summary>
        public string? CounterpartyClass { get; set; }
        /// <summary>"ymin,xmin,ymax,xmax", normalised 0–1000, for evidence highlighting.</summary>
        public string? Box { get; set; }
    }

    /// <summary>Per-page balances and stated totals — PAGE_CARRY_MISMATCH and TOTALS_MISMATCH.</summary>
    public class PageSummary
    {
        public int CaseDocumentID { get; set; }
        public int PageNo { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
        public decimal? StatedTotalDebit { get; set; }
        public decimal? StatedTotalCredit { get; set; }
    }

    /// <summary>A named field from a salary slip, KYC document or ITR.</summary>
    public class DocField
    {
        public long DocFieldID { get; set; }
        public int CaseDocumentID { get; set; }
        /// <summary>employee_name, employer, gross, net, pan_masked, dob, address…</summary>
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal? NumValue { get; set; }
        public int PageNo { get; set; }
        public string? Box { get; set; }
    }
}
