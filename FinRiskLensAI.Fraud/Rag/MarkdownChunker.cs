using System.Text.RegularExpressions;
using FinRiskLensAI.Core.Models.Knowledge;

namespace FinRiskLensAI.Fraud.Rag
{
    /// <summary>A chunk before it is embedded and stored as a <see cref="KnowledgeChunk"/>.</summary>
    public sealed record ChunkDraft(int Ordinal, string Breadcrumb, string Text, int TokenCount)
    {
        /// <summary>
        /// What gets embedded. The breadcrumb is prepended so a query phrased in
        /// different words from the clause body still lands on it (§6, contextual prefixing).
        /// </summary>
        public string EmbeddingText => Breadcrumb + "\n\n" + Text;
    }

    /// <summary>
    /// Splits a parsed knowledge document (markdown) into retrievable chunks —
    /// Doc/11_RAG_ARCHITECTURE.md §6. One splitter, configured per corpus:
    /// heading-aware packing for policy and regulation, one whole chunk per entry
    /// for the atomic corpora.
    ///
    /// The unit that is never split is the <b>clause</b>: a numbered clause together
    /// with its continuation lines and sub-items. Splitting "…is permitted unless:"
    /// from "(a) the applicant has prior default" inverts the rule. Tables are one
    /// clause each, so an exposure-limits table is never read half.
    /// </summary>
    public static class MarkdownChunker
    {
        /// <param name="MaxTokens">Pack clauses up to this size. A single clause larger than this is kept whole.</param>
        /// <param name="OverlapTokens">Trailing clauses of the previous chunk repeated at the start of the next, within one section.</param>
        /// <param name="MaxSplitLevel">Headings at or above this depth start a new section; deeper ones stay inside the text.</param>
        public sealed record Options(int MaxTokens, int OverlapTokens, int MaxSplitLevel);

        public static Options For(KnowledgeCorpus corpus) => corpus switch
        {
            KnowledgeCorpus.Policy => new(700, 80, 6),
            KnowledgeCorpus.Regulation => new(800, 100, 6),
            // Typology, case memory, exemplars: an entry is atomic. Entries are the
            // # / ## sections; their ### sub-headings ("Red flags") stay in the entry.
            _ => new(int.MaxValue, 0, 2)
        };

        // ponytail: ~4 chars per token for English. Swap for the embedding model's
        // CountTokens call if chunk budgets ever need to be exact.
        public static int EstimateTokens(string text) => (text.Length + 3) / 4;

        private static readonly Regex Heading = new(@"^(#{1,6})\s+(.+?)\s*#*$");
        // 4 · 4.2 · 4.2.1 · 12. · 3)
        private static readonly Regex Clause = new(@"^\d+(\.\d+)*[.)]?\s+\S");
        // (a) · (iv) · a. · b) · - · * · • · or anything indented
        private static readonly Regex SubItem = new(@"^(\s+\S|\([a-z0-9]{1,4}\)\s|[a-z][.)]\s|[-*+•]\s)", RegexOptions.IgnoreCase);

        public static List<ChunkDraft> Chunk(string title, string markdown, Options options)
        {
            var chunks = new List<ChunkDraft>();
            var headings = new List<(int Level, string Text)>();
            var clauses = new List<string>();   // the current section's clauses
            var clause = new List<string>();    // the current clause's lines
            bool prevBlank = false, inTable = false;

            void EndClause()
            {
                if (clause.Count > 0) clauses.Add(string.Join("\n", clause).Trim());
                clause.Clear();
                inTable = false;
            }

            void EndSection()
            {
                EndClause();
                var crumb = string.Join(" › ", headings.Select(h => h.Text).Prepend(title));
                Pack(clauses, crumb, options, chunks);
                clauses.Clear();
            }

            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();

                var h = Heading.Match(line);
                if (h.Success)
                {
                    int level = h.Groups[1].Value.Length;
                    if (level <= options.MaxSplitLevel)
                    {
                        EndSection();
                        headings.RemoveAll(x => x.Level >= level);
                        headings.Add((level, h.Groups[2].Value));
                    }
                    else
                    {
                        // A sub-heading inside an atomic entry opens a clause of its own
                        EndClause();
                        clause.Add(line);
                    }
                    prevBlank = false;
                    continue;
                }

                if (line.Length == 0)
                {
                    prevBlank = true;
                    continue;
                }

                bool isTable = line.TrimStart().StartsWith('|');
                if (clause.Count > 0 && StartsNewClause(line, clause[^1], isTable, inTable, prevBlank))
                    EndClause();
                else if (clause.Count > 0 && prevBlank)
                    clause.Add(string.Empty);   // keep the paragraph break inside the clause

                clause.Add(line);
                inTable = isTable;
                prevBlank = false;
            }
            EndSection();
            return chunks;
        }

        private static bool StartsNewClause(string line, string last, bool isTable, bool inTable, bool prevBlank)
        {
            // An introduction ("…permitted unless:") owns what follows it, table or list
            if (last.EndsWith(':')) return false;
            if (isTable || inTable) return isTable != inTable;   // a table starts and ends a clause
            if (SubItem.IsMatch(line)) return false;
            if (Clause.IsMatch(line)) return true;
            return prevBlank;
        }

        private static void Pack(List<string> clauses, string crumb, Options options, List<ChunkDraft> chunks)
        {
            var current = new List<string>();
            long currentTokens = 0;

            foreach (var c in clauses)
            {
                int tokens = Cost(c);
                if (current.Count > 0 && currentTokens + tokens > options.MaxTokens)
                {
                    Emit(current, crumb, chunks);

                    // Overlap by whole clauses, never the whole previous chunk, and only
                    // while the next chunk still fits.
                    var carry = new List<string>();
                    long carryTokens = 0;
                    for (int i = current.Count - 1; i > 0; i--)
                    {
                        int t = Cost(current[i]);
                        if (carryTokens + t > options.OverlapTokens) break;
                        carry.Insert(0, current[i]);
                        carryTokens += t;
                    }
                    if (carryTokens + tokens > options.MaxTokens) { carry.Clear(); carryTokens = 0; }

                    current = carry;
                    currentTokens = carryTokens;
                }
                current.Add(c);
                currentTokens += tokens;
            }
            if (current.Count > 0) Emit(current, crumb, chunks);
        }

        // A clause's share of the budget, including the paragraph break that joins it —
        // so the packed sum never under-counts the emitted chunk.
        private static int Cost(string clause) => EstimateTokens(clause) + 1;

        private static void Emit(List<string> clauses, string crumb, List<ChunkDraft> chunks)
        {
            var text = string.Join("\n\n", clauses);
            chunks.Add(new ChunkDraft(chunks.Count, crumb, text, EstimateTokens(text)));
        }
    }
}
