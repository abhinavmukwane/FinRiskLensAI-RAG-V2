namespace FinRiskLensAI.Core.Models.Storage
{
    /// <summary>
    /// Naming convention for files inside an MSME's blob folder. Deterministic names
    /// let the engine route each file to the right extractor without guessing.
    /// </summary>
    public static class MsmeDataFiles
    {
        public const string Manifest = "_manifest.json";
        /// <summary>
        /// V1's persisted score. Nothing writes it any more, but folders copied from V1
        /// demo data still carry one, so it is excluded from the data-file listing.
        /// </summary>
        public const string LegacyResult = "result.json";

        public const string Udyam = "udyam.json";
        public const string Itr = "itr.json";
        public const string Aa = "aa.json";
        public const string GstTaxpayer = "gst_taxpayer.json";
        public const string Epfo = "epfo.json";
        public const string Mca = "mca.json";

        // Per-director DIN verification files, e.g. DIN_85111678.json (one per company director).
        public const string DinPrefix = "DIN_";
        public static string DinFile(string din) => $"{DinPrefix}{din?.Trim()}.json";

        // Monthly GST files carry a MMyyyy period suffix, e.g. gstr3b_012026.json
        public const string Gstr3bPrefix = "gstr3b_";
        public const string Gstr1SummaryPrefix = "gstr1_summary_";
        public const string Gstr1B2bPrefix = "gstr1_b2b_";          // also matches gstr1_b2b_Invoice_MMyyyy.json
        public const string Gstr1CdnrPrefix = "gstr1_cdnr_";
        public const string Gstr1HsnPrefix = "gstr1_hsn_summary_";
        public const string Gstr2aB2bPrefix = "gstr2a_b2b_";
        /// <summary>Auto-drafted ITC statement, e.g. gstr2b_get_all_details_MMyyyy.json.</summary>
        public const string Gstr2bPrefix = "gstr2b_";

        private static readonly string[] KnownFixed = { Udyam, Itr, Aa, GstTaxpayer, Epfo, Mca, Manifest, LegacyResult };
        private static readonly string[] KnownPrefixes =
            { Gstr3bPrefix, Gstr1SummaryPrefix, Gstr1B2bPrefix, Gstr1CdnrPrefix, Gstr1HsnPrefix, Gstr2aB2bPrefix, Gstr2bPrefix, DinPrefix };

        public static bool IsKnown(string fileName)
            => KnownFixed.Contains(fileName, StringComparer.OrdinalIgnoreCase)
            || KnownPrefixes.Any(p => fileName.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }
}
