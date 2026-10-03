namespace FinRiskLensAI.Core.Models.Cases
{
    // The extractor's output contract — what Gemini returns for one document (reference
    // design §12.1) and what the demo generator writes as ground truth. Numbers are as
    // printed, never corrected: tampered arithmetic is the evidence.

    public class ExtractedDocument
    {
        public CaseDocType DocType { get; set; }
        /// <summary>The bank, employer or authority the document claims to come from.</summary>
        public string? ClaimedIssuer { get; set; }
        /// <summary>Template description with no names, numbers or dates — the fingerprint source.</summary>
        public string? LayoutText { get; set; }
        public List<ExtractedPage> Pages { get; set; } = new();
        public List<ExtractedField> Fields { get; set; } = new();
    }

    public class ExtractedPage
    {
        public int PageNo { get; set; }
        public decimal? OpeningBalance { get; set; }
        public decimal? ClosingBalance { get; set; }
        public decimal? StatedTotalDebit { get; set; }
        public decimal? StatedTotalCredit { get; set; }
        public List<ExtractedRow> Rows { get; set; } = new();
    }

    public class ExtractedRow
    {
        public DateOnly Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal? Debit { get; set; }
        public decimal? Credit { get; set; }
        public decimal? Balance { get; set; }
        public string? CounterpartyName { get; set; }
        public string? CounterpartyAccount { get; set; }
        /// <summary>"ymin,xmin,ymax,xmax", normalised 0–1000.</summary>
        public string? Box { get; set; }
    }

    public class ExtractedField
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public decimal? NumValue { get; set; }
        public int PageNo { get; set; } = 1;
        public string? Box { get; set; }
    }
}
