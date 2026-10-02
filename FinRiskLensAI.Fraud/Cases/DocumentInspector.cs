using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace FinRiskLensAI.Fraud.Cases
{
    /// <summary>
    /// What can be learned from an uploaded file before any model reads it: its real
    /// type and, for PDFs, the producer, dates and fonts METADATA_ANOMALY checks.
    /// </summary>
    public static class DocumentInspector
    {
        public sealed record PdfMetadata(int? PageCount, string? Producer, string? Creator,
                                         DateTime? CreatedAt, DateTime? ModifiedAt, string? FontSet);

        private static readonly PdfMetadata Unreadable = new(null, null, null, null, null, null);
        private static readonly Regex SubsetPrefix = new(@"^[A-Z]{6}\+");

        /// <summary>
        /// Type from the leading bytes — never the file name or the browser's claimed
        /// type, because the upload is a trust boundary. Null means not accepted.
        /// </summary>
        public static (string ContentType, string Extension)? Sniff(ReadOnlySpan<byte> bytes)
        {
            if (bytes.StartsWith("%PDF-"u8)) return ("application/pdf", ".pdf");
            if (bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return ("image/png", ".png");
            if (bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return ("image/jpeg", ".jpg");
            return null;
        }

        /// <summary>
        /// Reads PDF metadata. A PDF PdfPig cannot open — password-protected bank
        /// e-statements are the common case — still uploads, with metadata left empty.
        /// </summary>
        public static PdfMetadata ReadPdf(byte[] bytes)
        {
            try
            {
                using var pdf = PdfDocument.Open(bytes);
                var info = pdf.Information;

                var fonts = pdf.GetPages()
                    .SelectMany(p => p.Letters.Select(l => l.FontName))
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => SubsetPrefix.Replace(f, ""))
                    .Distinct()
                    .OrderBy(f => f, StringComparer.Ordinal);
                var fontSet = string.Join(",", fonts);

                return new PdfMetadata(
                    pdf.NumberOfPages,
                    Clip(info.Producer, 200),
                    Clip(info.Creator, 200),
                    info.GetCreatedDateTimeOffset()?.UtcDateTime,
                    info.GetModifiedDateTimeOffset()?.UtcDateTime,
                    fontSet.Length == 0 ? null : Clip(fontSet, 1000));
            }
            catch (Exception)
            {
                return Unreadable;
            }
        }

        private static string? Clip(string? s, int max)
            => string.IsNullOrWhiteSpace(s) ? null : s.Length <= max ? s.Trim() : s[..max].Trim();
    }
}
