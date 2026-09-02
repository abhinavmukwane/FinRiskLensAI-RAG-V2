namespace FinRiskLensAI.Core.Models.GST
{
    /// <summary>
    /// ViewModel for /Dashboard/GSTAnalysis. Populated by DashboardController
    /// from IGSTR2And3BResponceService.GetResponces() (latest t_GSTR2And3BResponce
    /// row for the session's UdyamNumber). Phase 1 carries the raw GSTR-2B/3B
    /// response JSON through to the page; the BI dashboard renders on top of it
    /// in the next phase. Never expose the EF entity directly to the view.
    /// </summary>
    public class GSTAnalysisViewModel
    {
        public string? Uan { get; set; }
        public string? LoadError { get; set; }
        public bool HasData { get; set; }

        /// <summary>
        /// Enterprise identity carried on the model rather than read from session,
        /// so the same view renders for a bank officer reviewing another MSME's file.
        /// </summary>
        public string? EnterpriseName { get; set; }
        public string? PanNumber { get; set; }

        public string? Gstin { get; set; }
        public string? FilingPeriod { get; set; }
        public DateTime CreatedDate { get; set; }

        /// <summary>Raw GSTR-2B response JSON as stored (2BResponseData).</summary>
        public string? GSTR2BResponseData { get; set; }

        /// <summary>Raw GSTR-3B response JSON as stored (3BResponseData).</summary>
        public string? GSTR3BResponseData { get; set; }
    }
}
