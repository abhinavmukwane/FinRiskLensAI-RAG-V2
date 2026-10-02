using System.Text;
using FinRiskLensAI.Fraud.Cases;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FinRiskLensAI.Tests
{
    public class DocumentInspectorTests
    {
        [Fact]
        public void Type_comes_from_the_bytes_not_the_name()
        {
            Assert.Equal("application/pdf", DocumentInspector.Sniff(Encoding.ASCII.GetBytes("%PDF-1.7\n…"))?.ContentType);
            Assert.Equal("image/png", DocumentInspector.Sniff(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 })?.ContentType);
            Assert.Equal("image/jpeg", DocumentInspector.Sniff(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })?.ContentType);

            // An HTML page renamed statement.pdf is refused
            Assert.Null(DocumentInspector.Sniff(Encoding.ASCII.GetBytes("<html><script>")));
            Assert.Null(DocumentInspector.Sniff(Array.Empty<byte>()));
        }

        [Fact]
        public void Pdf_metadata_is_read()
        {
            var builder = new PdfDocumentBuilder();
            builder.DocumentInformation.Producer = "iText 7.2 (ABC Bank e-Statement)";
            builder.DocumentInformation.Creator = "Statement Generator";
            var helvetica = builder.AddStandard14Font(Standard14Font.Helvetica);
            var courier = builder.AddStandard14Font(Standard14Font.Courier);
            var page = builder.AddPage(595, 842);
            page.AddText("Opening balance 1,20,000.00", 10, new PdfPoint(40, 800), helvetica);
            page.AddText("Closing balance 1,48,250.00", 10, new PdfPoint(40, 780), courier);
            builder.AddPage(595, 842);

            var meta = DocumentInspector.ReadPdf(builder.Build());

            Assert.Equal(2, meta.PageCount);
            Assert.Equal("iText 7.2 (ABC Bank e-Statement)", meta.Producer);
            Assert.Equal("Statement Generator", meta.Creator);
            Assert.Equal("Courier,Helvetica", meta.FontSet);
        }

        [Fact]
        public void An_unreadable_pdf_still_uploads_with_empty_metadata()
        {
            var meta = DocumentInspector.ReadPdf(Encoding.ASCII.GetBytes("%PDF-1.7\nthis is not a pdf"));
            Assert.Null(meta.PageCount);
            Assert.Null(meta.Producer);
        }
    }
}
