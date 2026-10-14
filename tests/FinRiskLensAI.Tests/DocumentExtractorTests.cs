using FinRiskLensAI.Fraud.Ai;
using static FinRiskLensAI.Fraud.Ai.DocumentExtractor;

namespace FinRiskLensAI.Tests
{
    public class DocumentExtractorTests
    {
        private static RawDocument Page(decimal opening, decimal? closing, decimal? totalDebit, params (decimal? Debit, decimal? Credit, decimal Balance)[] rows) => new()
        {
            Pages = new()
            {
                new RawPage
                {
                    PageNo = 1, OpeningBalance = opening, ClosingBalance = closing, StatedTotalDebit = totalDebit,
                    Rows = rows.Select(r => new RawRow { Date = "2026-08-09", Debit = r.Debit, Credit = r.Credit, Balance = r.Balance }).ToList()
                }
            }
        };

        [Fact]
        public void A_read_that_adds_up_is_kept_and_one_that_does_not_is_read_again()
        {
            Assert.True(AddsUp(Page(1_000, 1_300, 200, (null, 500, 1_500), (200, null, 1_300))));
            Assert.False(AddsUp(Page(1_000, 1_300, 200, (null, 500, 1_500), (200, null, 1_330))));   // a misread digit
            Assert.False(AddsUp(Page(1_000, 1_300, 200, (null, 500, 1_500))));                        // a dropped row
            Assert.True(AddsUp(new RawDocument { Fields = new() { new RawField { Name = "gross", Value = "1" } } }));   // not a statement
        }
    }
}
