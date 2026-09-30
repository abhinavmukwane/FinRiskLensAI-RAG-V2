using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Core.Models.Storage;
using FinRiskLensAI.ML.Features;
using FinRiskLensAI.ML.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FinRiskLensAI.Tests
{
    /// <summary>
    /// Pins <see cref="SourceFactsService"/> to what the V1 scorer produced. The
    /// fixtures were captured from RiskScoringService on 2026-09-30, immediately before
    /// it was deleted, over the same demo folders. Any drift in extraction, file
    /// routing or the deep-dives fails here — the fraud checks are built on these facts.
    /// </summary>
    public class SourceFactsGoldenTests
    {
        public static IEnumerable<object[]> DemoAccounts()
            => Directory.GetFiles(FixtureDir, "*.facts.json")
                .Select(f => new object[] { Path.GetFileName(f).Replace(".facts.json", "") });

        [Theory]
        [MemberData(nameof(DemoAccounts))]
        public void Facts_match_the_v1_scorer_output(string uan)
        {
            var folder = Path.Combine(RepoRoot, "Doc", "demo-data", uan);
            var files = Directory.GetFiles(folder)
                .Select(Path.GetFileName)
                .Where(f => f != MsmeDataFiles.Manifest && f != MsmeDataFiles.LegacyResult)
                .ToDictionary(f => f!, f => (string?)File.ReadAllText(Path.Combine(folder, f!)));

            var service = new SourceFactsService(new UdyamFeatureExtractor(), new ItrFeatureExtractor(),
                new GstFeatureExtractor(), new AaFeatureExtractor());
            var facts = service.Build(SourceFactsRequest.FromFiles(files));

            var actual = JToken.Parse(JsonConvert.SerializeObject(
                new { facts.BankAnalysis, facts.GstAnalysis, facts.Financials, facts.Industry }));
            var expected = JToken.Parse(File.ReadAllText(Path.Combine(FixtureDir, $"{uan}.facts.json")));

            Assert.True(JToken.DeepEquals(expected, actual),
                $"{uan}: facts drifted from the V1 baseline.\nexpected: {expected}\nactual:   {actual}");
        }

        [Fact]
        public void Every_fixture_has_all_four_fact_blocks()
        {
            // Guards the fixtures themselves: a baseline captured from an empty or partial
            // folder would make the golden test pass vacuously.
            foreach (var file in Directory.GetFiles(FixtureDir, "*.facts.json"))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                foreach (var block in new[] { "BankAnalysis", "GstAnalysis", "Financials", "Industry" })
                    Assert.True(json[block]?.Type == JTokenType.Object, $"{Path.GetFileName(file)} is missing {block}");
            }
        }

        private static string FixtureDir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "facts");

        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
                return dir?.FullName ?? throw new InvalidOperationException("Solution root not found.");
            }
        }
    }
}
