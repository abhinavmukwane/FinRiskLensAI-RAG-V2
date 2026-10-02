using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Fraud.Rag;

namespace FinRiskLensAI.Tests
{
    /// <summary>R2 — Doc/11_RAG_ARCHITECTURE.md §17: clause boundaries survive chunking.</summary>
    public class ChunkerTests
    {
        private const string Policy = """
            # §4 Exposure Norms

            Exposure is measured on sanctioned limits, not outstanding balances.

            ## §4.2 Gambling and speculative spend

            4.2.1 Spend on betting or crypto platforms is permitted unless:
            (a) it exceeds 5% of average monthly credits; or
            (b) the applicant has a prior default in the last 24 months.

            4.2.2 Any case breaching 4.2.1 requires senior credit approval and a
            documented rationale on the case file.

            4.2.3 Exposure limits by segment:

            | Segment | Max exposure | Approver |
            |---|---|---|
            | Micro | ₹25 lakh | Branch head |
            | Small | ₹2 crore | Regional credit |
            | Medium | ₹10 crore | Zonal credit committee |

            4.2.4 Limits are reviewed annually.
            """;

        private static List<ChunkDraft> Tiny(string md) =>
            MarkdownChunker.Chunk("Credit Policy v3", md, new MarkdownChunker.Options(MaxTokens: 10, OverlapTokens: 0, MaxSplitLevel: 6));

        [Fact]
        public void Clause_with_sub_items_is_never_split_even_when_over_budget()
        {
            var chunk = Tiny(Policy).Single(c => c.Text.Contains("permitted unless:"));

            Assert.Contains("(a) it exceeds 5%", chunk.Text);
            Assert.Contains("(b) the applicant has a prior default", chunk.Text);
            Assert.DoesNotContain("4.2.2", chunk.Text);
        }

        [Fact]
        public void Wrapped_clause_stays_whole()
        {
            var chunk = Tiny(Policy).Single(c => c.Text.Contains("4.2.2"));
            Assert.Contains("documented rationale", chunk.Text);
        }

        [Fact]
        public void Table_is_kept_whole_with_its_introduction()
        {
            var chunk = Tiny(Policy).Single(c => c.Text.Contains("| Micro"));

            Assert.Contains("4.2.3 Exposure limits by segment:", chunk.Text);
            Assert.Contains("| Medium | ₹10 crore |", chunk.Text);
            Assert.DoesNotContain("4.2.4", chunk.Text);
        }

        [Fact]
        public void Breadcrumb_follows_the_heading_path()
        {
            var chunks = Tiny(Policy);

            Assert.Equal("Credit Policy v3 › §4 Exposure Norms",
                chunks.Single(c => c.Text.Contains("sanctioned limits")).Breadcrumb);
            Assert.Equal("Credit Policy v3 › §4 Exposure Norms › §4.2 Gambling and speculative spend",
                chunks.Single(c => c.Text.Contains("4.2.4")).Breadcrumb);
        }

        [Fact]
        public void No_text_is_lost()
        {
            var all = string.Join("\n", Tiny(Policy).Select(c => c.Text));
            var body = Policy.Split('\n').Select(l => l.Trim())
                             .Where(l => l.Length > 0 && !l.StartsWith('#'));

            Assert.All(body, line => Assert.Contains(line, all));
        }

        [Fact]
        public void Packing_respects_the_budget_and_overlaps_by_whole_clauses()
        {
            var md = "## §7 Verification\n\n" + string.Join("\n\n",
                Enumerable.Range(1, 40).Select(i => $"7.{i} Verification step {i} must be recorded with the officer's id and the timestamp of the check."));

            var opts = new MarkdownChunker.Options(MaxTokens: 120, OverlapTokens: 30, MaxSplitLevel: 6);
            var chunks = MarkdownChunker.Chunk("SOP", md, opts);

            Assert.True(chunks.Count > 1);
            Assert.All(chunks, c => Assert.True(c.TokenCount <= opts.MaxTokens, $"chunk {c.Ordinal} is {c.TokenCount} tokens"));
            for (int i = 1; i < chunks.Count; i++)
            {
                var lastClauseOfPrevious = chunks[i - 1].Text.Split("\n\n")[^1];
                Assert.StartsWith(lastClauseOfPrevious, chunks[i].Text);
            }
            Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(c => c.Ordinal));
        }

        [Fact]
        public void Atomic_corpus_gives_one_chunk_per_entry_with_its_sub_headings()
        {
            const string typology = """
                # Fraud Typology Catalogue

                ## T-01 Salary round-tripping
                Salary is credited and sent back to the payer within days.
                ### Red flags
                - Debit to the salary payer within 72 hours of the credit

                ## T-02 Window dressing
                Large credits land just before the application, then leave.
                ### Red flags
                - Average balance in the last 15 days over twice the prior 75
                """;

            var chunks = MarkdownChunker.Chunk("Typology", typology, MarkdownChunker.For(KnowledgeCorpus.Typology));

            Assert.Equal(2, chunks.Count);
            Assert.Equal("Typology › Fraud Typology Catalogue › T-01 Salary round-tripping", chunks[0].Breadcrumb);
            Assert.Contains("### Red flags", chunks[0].Text);
            Assert.Contains("within 72 hours", chunks[0].Text);
            Assert.Contains("twice the prior 75", chunks[1].Text);
        }

        [Fact]
        public void Embedding_text_is_prefixed_with_the_breadcrumb()
        {
            var c = Tiny(Policy)[0];
            Assert.StartsWith(c.Breadcrumb + "\n\n", c.EmbeddingText);
        }
    }
}
