using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Data.DbContextEDMX;
using FinRiskLensAI.Data.Repositories.Knowledge;
using FinRiskLensAI.Fraud.Ai;
using FinRiskLensAI.Fraud.Rag;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

// RAG harness — Doc/11_RAG_ARCHITECTURE.md §17: build and judge retrieval with no UI.
// Run from the repository root; it reads the same connection string the app does.
//
//   ingest <corpus> <file.md> --title "<title>" [--version <v>]
//   search <corpus[,corpus…]> <question…> [--k <n>]
//   embed                      vectors for every current chunk that lacks one (backfill)
//
// Corpora: Policy, Regulation, Typology, CaseMemory, Exemplar.

if (args.Length == 0 || args[0] is not ("ingest" or "search" or "embed") || (args[0] != "embed" && args.Length < 3))
{
    Console.Error.WriteLine("""
        usage:
          ingest <corpus> <file.md> --title "<title>" [--version <v>]
          search <corpus[,corpus…]> <question…> [--k <n>]
          embed
        """);
    return 2;
}

string? Option(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static KnowledgeCorpus Corpus(string s) => Enum.Parse<KnowledgeCorpus>(s.Trim(), ignoreCase: true);

await using var db = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
var config = ApplicationDbContextFactory.BuildConfiguration();
var geminiOptions = config.GetSection("Gemini").Get<GeminiOptions>() ?? new GeminiOptions();
geminiOptions.ProjectId = config["Gcp:ProjectId"] ?? string.Empty;
var knowledge = new KnowledgeService(new KnowledgeRepository(db), new RagOptions(),
                                     new GeminiClient(geminiOptions, NullLogger<GeminiClient>.Instance));

if (args[0] == "embed")
{
    Console.WriteLine($"embedded {await knowledge.EmbedMissingAsync()} chunk(s) with {geminiOptions.EmbeddingModel}");
    return 0;
}

if (args[0] == "ingest")
{
    var title = Option("--title");
    if (title == null) { Console.Error.WriteLine("--title is required: it roots every citation."); return 2; }

    var result = await knowledge.IngestMarkdownAsync(Corpus(args[1]), title, Option("--version"),
                                                     await File.ReadAllTextAsync(args[2]), "rag-cli");
    Console.WriteLine(result.AlreadyCurrent
        ? $"unchanged — document {result.DocId} already holds these bytes"
        : $"document {result.DocId}: {result.Chunks} chunks, {result.Superseded} previous version(s) retired");
    return 0;
}

// Everything after the corpora that is not an option is the question
var words = new List<string>();
for (int i = 2; i < args.Length; i++)
{
    if (args[i].StartsWith("--")) { i++; continue; }
    words.Add(args[i]);
}
var k = int.TryParse(Option("--k"), out var n) ? n : (int?)null;
var hits = await knowledge.SearchAsync(string.Join(' ', words), args[1].Split(',').Select(Corpus).ToList(), k);

if (hits.Count == 0) { Console.WriteLine("no match"); return 1; }
foreach (var (h, i) in hits.Select((h, i) => (h, i + 1)))
{
    var text = h.Text.ReplaceLineEndings(" ");
    Console.WriteLine($"{i}. [{h.Corpus}] {h.Breadcrumb}   (chunk {h.ChunkId}, rrf {h.Score:0.0000}, keyword #{h.SparseRank?.ToString() ?? "-"}, vector #{h.DenseRank?.ToString() ?? "-"}, relevance {h.Relevance?.ToString("0.00") ?? "-"})");
    Console.WriteLine($"   {(text.Length > 220 ? text[..220] + "…" : text)}");
}
return 0;
