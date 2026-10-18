using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.ML.Features;
using FinRiskLensAI.ML.Services;

namespace FinRiskLensAI.Tests
{
    public class FinancialHealthScoreTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
                return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
            }
        }

        [Fact]
        public void Score_computes_six_dimensions_and_band_for_demo_account()
        {
            var uan = "UDYAM-MH-20-0091447";
            var folder = Path.Combine(RepoRoot, "Doc", "demo-data", uan);
            var files = Directory.GetFiles(folder)
                .Select(Path.GetFileName)
                .Where(f => f != MsmeDataFiles.Manifest && f != MsmeDataFiles.LegacyResult)
                .ToDictionary(f => f!, f => (string?)File.ReadAllText(Path.Combine(folder, f!)));

            var factsService = new SourceFactsService(new UdyamFeatureExtractor(), new ItrFeatureExtractor(),
                new GstFeatureExtractor(), new AaFeatureExtractor());
            var facts = factsService.Build(SourceFactsRequest.FromFiles(files));

            var scorer = new RiskScoringService();
            var result = scorer.Analyze(facts);

            Assert.NotNull(result);
            Assert.True(result.OverallScore >= 0 && result.OverallScore <= 1000, $"Score {result.OverallScore} out of range");
            Assert.Equal(6, result.Dimensions.Count);
            Assert.NotNull(result.Lending);
            Assert.True(result.Lending.Ratios.Count > 0);
            Assert.True(result.Recommendations.Count > 0);
        }

        [Fact]
        public void Lending_calculator_computes_turnover_and_annuity_limits()
        {
            var features = new MsmeFeatureSet
            {
                GstAnnualisedTurnover = 50_000_000,
                GstMonthlyAvgTurnover = 4_166_666,
                GstMonthlyAvgPurchases = 3_000_000,
                HasGst = true,
                HasAa = true,
                AaAvgMonthlyCredit = 4_000_000,
                AaEmiMonthlyOutflow = 200_000,
                AaEmiToInflowRatio = 0.05,
                AaDaysCashOnHand = 45,
                AaInflowVolatility = 0.20
            };

            var lending = LendingCalculator.Compute(features, ScoreBandType.Good);

            Assert.NotNull(lending);
            Assert.True(lending.WorkingCapitalLimit > 0);
            Assert.True(lending.TermLoanCapacity > 0);
            Assert.True(lending.TotalIndicativeEligibility > 0);
            Assert.True(lending.Ratios.Count >= 5);
        }
    }
}
