using FinRiskLensAI.DemoData;
using FinRiskLensAI.Fraud.Checks;

namespace FinRiskLensAI.Tests
{
    /// <summary>
    /// The demo cases through the checks, in memory: genuine cases raise nothing, every
    /// tampered or behavioural case raises exactly its expected findings. Guards both the
    /// generator and the checks — a threshold change that floods genuine cases fails here.
    /// </summary>
    public class DemoScenarioTests
    {
        public static IEnumerable<object[]> Cases() => Scenarios.All().Select(c => new object[] { c.Ref });

        private static readonly Lazy<Dictionary<string, DemoCase>> All = new(() => Scenarios.All().ToDictionary(c => c.Ref));

        [Theory]
        [MemberData(nameof(Cases))]
        public void Case_raises_exactly_its_expected_findings(string reference)
        {
            var c = All.Value[reference];
            Assert.Equal(c.Expected.OrderBy(t => t), DryRun.FindingTypes(c, new FraudOptions()));
        }

        [Fact]
        public void The_network_lens_finds_exactly_the_planted_rings()
        {
            static string Key(IEnumerable<string> refs) => string.Join(",", refs.OrderBy(r => r));
            var planted = All.Value.Values.Where(c => c.Ring != null).GroupBy(c => c.Ring).Select(g => Key(g.Select(c => c.Ref)));
            var found = NetworkLens.Find(DryRun.Network(Scenarios.All())).Select(r => Key(r.Members.Select(m => m.ApplicantRef)));

            Assert.Equal(planted.OrderBy(k => k), found.OrderBy(k => k));
        }
    }
}
