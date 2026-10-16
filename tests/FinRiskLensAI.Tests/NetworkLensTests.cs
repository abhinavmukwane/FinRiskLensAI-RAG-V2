using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Fraud.Checks;

namespace FinRiskLensAI.Tests
{
    public class NetworkLensTests
    {
        private static NetworkApplication Business(int id, string uan, string name, string gstin, string mobile, string email, string address,
                                                   string[] dins, string[] invoices)
            => new(id, uan, name, Array.Empty<CaseDocument>(), Array.Empty<StatementRow>(), uan,
                   new CustomerEntities(gstin, mobile, email, address, dins.Select(d => new Director(d, "Director " + d)).ToList(), invoices));

        [Fact]
        public void Businesses_that_trade_in_a_loop_or_share_a_director_or_mobile_form_one_ring()
        {
            var apps = new[]
            {
                Business(1, "U1", "Alpha", "G1", "98765 43210", "a@x.in", "Plot 4, Shree Complex, MIDC Road, Pune, 411019", new[] { "D1" }, new[] { "G2" }),
                Business(2, "U2", "Beta", "G2", "+91 9876543210", "b@x.in", "Shop 9, Laxmi Market, Station Road, Nagpur, 440001", new[] { "D2" }, new[] { "G3" }),
                Business(3, "U3", "Gamma", "G3", "9000000003", "c@x.in", "Pune 411001", new[] { "D1" }, new[] { "G1" }),
                // Sells to Alpha, but nobody in the loop sells back: a customer of the ring, not part of it
                Business(4, "U4", "Delta", "G4", "9000000004", "d@x.in", "Unit 2, Ganga Estate, Ring Road, Surat, 395002", Array.Empty<string>(), new[] { "G1" }),
                // Two assessments of one business: the same director twice is not a link
                Business(5, "U5", "Epsilon", "G5", "9000000005", "e@x.in", "", new[] { "D7" }, Array.Empty<string>()),
                Business(6, "U5", "Epsilon", "G5", "9000000005", "e@x.in", "", new[] { "D7" }, Array.Empty<string>()),
            };

            var ring = Assert.Single(NetworkLens.Find(apps));

            Assert.Equal(new[] { 1, 2, 3 }, ring.Members.Select(m => m.LoanApplicationID).OrderBy(i => i));
            Assert.Equal(new[] { "Circular trading", "Same director", "Same mobile" }, ring.Links.Select(l => l.Kind).OrderBy(k => k));
            Assert.Contains("Alpha → Beta", ring.Links.Single(l => l.Kind == "Circular trading").Detail);
            Assert.Equal(new[] { 1, 3 }, ring.Links.Single(l => l.Kind == "Same director").Members.Keys.OrderBy(i => i));
            Assert.Equal(RiskSections.Network, RiskSections.Of(NetworkLens.FindingFor(ring, 1).Type));
        }
    }
}
