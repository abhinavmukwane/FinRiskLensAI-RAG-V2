using FinRiskLensAI.Fraud.Rag;

namespace FinRiskLensAI.Tests
{
    public class RrfTests
    {
        [Fact]
        public void An_id_both_arms_found_beats_a_top_hit_from_one_arm()
        {
            var sparse = new long[] { 10, 20, 30 };
            var dense = new long[] { 40, 20, 50 };

            var fused = Rrf.Fuse(new[] { sparse, dense }, k: 60);

            Assert.Equal(20, fused[0].Id);                       // rank 2 in both
            Assert.Equal(new long[] { 20, 10, 40, 30, 50 }, fused.Select(f => f.Id));
            Assert.Equal(2.0 / 62, fused[0].Score, 12);
        }

        [Fact]
        public void A_single_arm_keeps_its_order()
        {
            var fused = Rrf.Fuse(new[] { new long[] { 3, 1, 2 } }, k: 60);
            Assert.Equal(new long[] { 3, 1, 2 }, fused.Select(f => f.Id));
        }

        [Fact]
        public void No_arms_no_results() => Assert.Empty(Rrf.Fuse(Array.Empty<long[]>(), k: 60));
    }
}
