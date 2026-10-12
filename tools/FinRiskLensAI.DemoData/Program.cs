using System.Text.Json;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Data.DbContextEDMX;
using FinRiskLensAI.Data.Repositories.Cases;
using FinRiskLensAI.DemoData;
using FinRiskLensAI.Fraud.Ai;
using FinRiskLensAI.Fraud.Cases;
using FinRiskLensAI.Fraud.Checks;
using FinRiskLensAI.ML.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

// Demo data — Doc/10_FRAUD_LENS_BUILD.md, Phase 6. Run from the repository root.
//
//   generate <dir>   synthetic PDFs + <file>.truth.json (what an exact extractor reads) + cases.json
//   seed             the same cases through real intake → truth applied as extraction → checks run,
//                    then expected vs actual findings per case. Exit code 1 on any mismatch.
//   investigate [ref…]  the live Investigator on seeded cases, scored against each case's ExpectedAi;
//                    genuine cases expect nothing, so anything raised there is a false positive
//   copy-to-gcs      one-off: every stored case document from the Azure container into Gcs:CasesBucket
//   eval [ref…]      live Gemini extraction of every demo document scored against its truth, then
//                    the checks run on what Gemini read — the end-to-end number.

if (args.Length == 0 || args[0] is not ("generate" or "seed" or "eval" or "copy-to-gcs" or "investigate") || (args[0] == "generate" && args.Length < 2))
{
    Console.Error.WriteLine("usage: generate <dir> | seed | eval [ref…] | investigate [ref…] | copy-to-gcs");
    return 2;
}

var cases = Scenarios.All();
var json = new JsonSerializerOptions { WriteIndented = true };

if (args[0] == "generate")
{
    var dir = Directory.CreateDirectory(args[1]).FullName;
    foreach (var c in cases)
    foreach (var d in c.Docs)
    {
        await File.WriteAllBytesAsync(Path.Combine(dir, d.FileName), d.Pdf);
        await File.WriteAllTextAsync(Path.Combine(dir, d.FileName + ".truth.json"), JsonSerializer.Serialize(d.Truth, json));
    }
    await File.WriteAllTextAsync(Path.Combine(dir, "cases.json"), JsonSerializer.Serialize(
        cases.Select(c => new { c.Ref, Product = c.Product.ToString(), c.Uan, c.Story, c.Expected, Files = c.Docs.Select(d => d.FileName) }), json));
    Console.WriteLine($"{cases.Sum(c => c.Docs.Count)} documents for {cases.Count} cases in {dir}");

    // Dry run: the same checks, in memory, so a mismatch shows before anything is written
    var diffs = 0;
    foreach (var c in cases)
    {
        var actual = DryRun.FindingTypes(c, new FraudOptions());
        var ok = actual.SequenceEqual(c.Expected.OrderBy(t => t));
        if (!ok) diffs++;
        Console.WriteLine($"{c.Ref,-9} {(ok ? "ok  " : "DIFF")} {(actual.Length == 0 ? "no findings" : string.Join(", ", actual))}" +
                          (ok ? "" : $"   expected: {(c.Expected.Length == 0 ? "none" : string.Join(", ", c.Expected.OrderBy(t => t)))}"));
    }
    return diffs == 0 ? 0 : 1;
}

var config = ApplicationDbContextFactory.BuildConfiguration();

if (args[0] == "investigate")
{
    var gemini = new GeminiClient(Gemini(config), NullLogger<GeminiClient>.Instance);
    await using var investigateDb = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
    var caseRepo = new CaseRepository(investigateDb);
    var knowledge = new FinRiskLensAI.Fraud.Rag.KnowledgeService(
        new FinRiskLensAI.Data.Repositories.Knowledge.KnowledgeRepository(investigateDb),
        new FinRiskLensAI.Core.Models.Knowledge.RagOptions(), gemini);
    var investigator = new Investigator(gemini, caseRepo, knowledge, NullLogger<Investigator>.Instance);

    var only = args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var seeded = (await caseRepo.ListAsync()).GroupBy(c => c.ApplicantRef).ToDictionary(g => g.Key, g => g.Max(c => c.LoanApplicationID));
    int hits = 0, misses = 0, falsePositives = 0;
    foreach (var c in cases.Where(c => only.Count == 0 || only.Contains(c.Ref)))
    {
        if (!seeded.TryGetValue(c.Ref, out var id)) { Console.WriteLine($"{c.Ref,-9} not seeded — run seed first"); continue; }
        InvestigationResult run;
        try { run = await investigator.InvestigateAsync(id); }
        catch (Exception ex)
        {
            Console.WriteLine($"{c.Ref,-9} case #{id,-3} FAILED: {ex.GetType().Name}: {ex.Message}");
            misses += c.ExpectedAi.Length;
            continue;
        }
        var ai = (await caseRepo.GetFindingsAsync(id)).Where(f => f.DetectedBy == FindingSource.AiVerified).ToList();
        var types = ai.Select(f => f.Type).Distinct().OrderBy(t => t).ToArray();
        var missing = c.ExpectedAi.Except(types).ToArray();
        var extra = types.Except(c.ExpectedAi).ToArray();
        hits += c.ExpectedAi.Length - missing.Length; misses += missing.Length;
        if (c.Ref.StartsWith("DEMO-G")) falsePositives += extra.Length;

        Console.WriteLine($"{c.Ref,-9} case #{id,-3} {run.Model} {run.Turns} turns {run.ToolCalls} calls {run.Took.TotalSeconds:0}s" +
                          $" | kept {run.Kept}, dropped {run.Dropped}, citations stripped {run.CitationsStripped}" +
                          (missing.Length > 0 ? $" | MISSING {string.Join(", ", missing)}" : "") +
                          (extra.Length > 0 ? $" | also {string.Join(", ", extra)}" : ""));
        foreach (var f in ai)
            Console.WriteLine($"   + {f.Type,-26} {f.Severity,-6} {f.Title}  [{f.Evidence.Count(e => e.Kind != EvidenceKind.Policy)} rows/fields, " +
                              $"{f.Evidence.Count(e => e.Kind == EvidenceKind.Policy)} policy]");
        foreach (var d in await caseRepo.GetLatestDropsAsync(id))
            Console.WriteLine($"   - dropped: {d.Reason}{Environment.NewLine}     {(d.RawFinding.Length <= 600 ? d.RawFinding : d.RawFinding[..600] + "…")}");
    }
    Console.WriteLine($"expected AI findings found: {hits}/{hits + misses}; raised on genuine cases: {falsePositives}");
    return misses == 0 && falsePositives == 0 ? 0 : 1;
}

if (args[0] == "copy-to-gcs")
{
    var bucket = config["Gcs:CasesBucket"];
    if (string.IsNullOrWhiteSpace(bucket)) { Console.Error.WriteLine("Gcs:CasesBucket is not configured."); return 2; }

    await using var source = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
    var azure = new AzureBlobDataStore(config);
    var gcs = new FinRiskLensAI.Fraud.Storage.GcsDocumentStore(bucket);
    var files = source.CaseDocuments.Select(d => new { d.StoragePath, d.ContentType }).Distinct().ToList();
    int copied = 0, missing = 0;
    foreach (var f in files)
    {
        await using var stream = await azure.OpenReadAsync(f.StoragePath);
        if (stream == null) { missing++; Console.WriteLine($"  missing in Azure: {f.StoragePath}"); continue; }
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        await gcs.PutAsync(f.StoragePath, buffer.ToArray(), f.ContentType);
        copied++;
    }
    Console.WriteLine($"{copied} file(s) in gs://{bucket}, {missing} missing");
    return missing == 0 ? 0 : 1;
}

if (args[0] == "eval")
{
    var gemini = new GeminiClient(Gemini(config), NullLogger<GeminiClient>.Instance);
    var extractor = new DocumentExtractor(gemini);
    var only = args.Skip(1).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var selected = cases.Where(c => only.Count == 0 || only.Contains(c.Ref)).ToList();
    Console.WriteLine($"extracting {selected.Sum(c => c.Docs.Count)} documents with {gemini.Options.ExtractModel} @ {gemini.Options.ExtractLocation}…");

    using var gate = new SemaphoreSlim(4);
    var results = new System.Collections.Concurrent.ConcurrentDictionary<string, (ExtractedDocument? Doc, string? Error, TimeSpan Took)>();
    await Task.WhenAll(selected.SelectMany(c => c.Docs).Select(async d =>
    {
        await gate.WaitAsync();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try { results[d.FileName] = (await extractor.ExtractAsync(d.Pdf, "application/pdf"), null, sw.Elapsed); }
        catch (Exception ex) { results[d.FileName] = (null, ex.Message, sw.Elapsed); }
        finally { gate.Release(); }
    }));

    int truthRows = 0, exactRows = 0, pageOk = 0, pageAll = 0, fieldOk = 0, fieldAll = 0, typeOk = 0, docs = 0, casesOk = 0;
    foreach (var c in selected)
    {
        foreach (var d in c.Docs)
        {
            var (got, error, took) = results[d.FileName];
            if (got == null) { Console.WriteLine($"  {d.FileName,-44} FAILED after {took.TotalSeconds:0}s: {error}"); continue; }
            var s = ExtractionEval.Compare(d.FileName, d.Truth, got);
            docs++; typeOk += s.TypeOk ? 1 : 0;
            truthRows += s.TruthRows; exactRows += s.RowsExact; pageOk += s.PageValuesOk; pageAll += s.PageValues;
            fieldOk += s.FieldsOk; fieldAll += s.Fields;
            Console.WriteLine($"  {d.FileName,-44} {took.TotalSeconds,4:0}s  type {(s.TypeOk ? "ok" : got.DocType.ToString())}" +
                              (s.TruthRows > 0 ? $"  rows {s.RowsExact}/{s.TruthRows} exact ({s.GotRows} read)  page values {s.PageValuesOk}/{s.PageValues}" : "") +
                              (s.Fields > 0 ? $"  fields {s.FieldsOk}/{s.Fields}" : ""));
        }

        var extractedDocs = c.Docs.Where(d => results[d.FileName].Doc != null).ToDictionary(d => d.FileName, d => results[d.FileName].Doc!);
        if (extractedDocs.Count < c.Docs.Count) continue;
        var actual = DryRun.FindingTypes(c, new FraudOptions(), extractedDocs);
        var ok = actual.SequenceEqual(c.Expected.OrderBy(t => t));
        if (ok) casesOk++;
        Console.WriteLine($"{c.Ref,-9} findings from Gemini's reading: {(ok ? "match" : "DIFFER")}  " +
                          $"{(actual.Length == 0 ? "none" : string.Join(", ", actual))}" +
                          (ok ? "" : $"   expected {(c.Expected.Length == 0 ? "none" : string.Join(", ", c.Expected.OrderBy(t => t)))}"));
    }

    static string Pct(int a, int b) => b == 0 ? "n/a" : $"{100.0 * a / b:0.0}% ({a}/{b})";
    Console.WriteLine();
    Console.WriteLine($"doc type       {Pct(typeOk, docs)}");
    Console.WriteLine($"rows exact     {Pct(exactRows, truthRows)}");
    Console.WriteLine($"page values    {Pct(pageOk, pageAll)}");
    Console.WriteLine($"slip fields    {Pct(fieldOk, fieldAll)}");
    Console.WriteLine($"cases whose findings match: {casesOk}/{selected.Count}");
    return casesOk == selected.Count ? 0 : 1;
}

// ── seed ──────────────────────────────────────────────────────────────────────────
var container = config["AzureBlob:Container"];
// V1's container holds live data under IDBI evaluation — this tool must never write there
if (string.IsNullOrWhiteSpace(container) || container == "msme-data")
{
    Console.Error.WriteLine($"Refusing to seed into blob container '{container}'. Set AzureBlob:Container to the fork's own container.");
    return 2;
}

await using var db = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
var repo = new CaseRepository(db);
// Same store choice as the app: Cloud Storage when a bucket is configured
var casesBucket = config["Gcs:CasesBucket"];
FinRiskLensAI.Core.Interfaces.ICaseDocumentStore store = string.IsNullOrWhiteSpace(casesBucket)
    ? new AzureBlobDataStore(config) : new FinRiskLensAI.Fraud.Storage.GcsDocumentStore(casesBucket);
var service = new CaseService(repo, store, new FraudOptions());
var existing = (await repo.ListAsync()).Select(c => c.ApplicantRef).ToHashSet();

int mismatches = 0;
foreach (var c in cases)
{
    if (existing.Contains(c.Ref)) { Console.WriteLine($"{c.Ref,-9} already seeded — skipped"); continue; }

    var created = await service.CreateAsync(new NewCaseRequest
    {
        ApplicantRef = c.Ref, ProductType = c.Product, Uan = c.Uan,
        DeclaredIncome = c.DeclaredIncome, DeclaredEmployer = c.Employer, DeclaredEmis = c.DeclaredEmis
    }, c.Docs.Select(d => new CaseUpload(d.FileName, d.Pdf)).ToList(), "demo-seed");
    if (created.Error != null) { Console.WriteLine($"{c.Ref,-9} intake refused: {created.Error}"); mismatches++; continue; }

    var id = created.LoanApplicationID!.Value;
    var application = (await repo.GetAsync(id))!;
    foreach (var d in c.Docs)
    {
        var docId = application.Documents.Single(x => x.FileName == d.FileName).CaseDocumentID;
        await service.ApplyExtractionAsync(id, docId, d.Truth, "demo-seed:truth");
    }
    await service.RunChecksAsync(id);

    var actual = (await repo.GetFindingsAsync(id)).Select(f => f.Type).Distinct().OrderBy(t => t).ToArray();
    var expected = c.Expected.OrderBy(t => t).ToArray();
    var ok = actual.SequenceEqual(expected);
    if (!ok) mismatches++;

    Console.WriteLine($"{c.Ref,-9} case #{id,-3} {(ok ? "ok  " : "DIFF")} {(actual.Length == 0 ? "no findings" : string.Join(", ", actual))}");
    if (!ok)
    {
        var missing = expected.Except(actual).ToArray();
        var extra = actual.Except(expected).ToArray();
        if (missing.Length > 0) Console.WriteLine($"          missing:    {string.Join(", ", missing)}");
        if (extra.Length > 0) Console.WriteLine($"          unexpected: {string.Join(", ", extra)}");
    }
}
Console.WriteLine(mismatches == 0 ? "all cases match their expected findings" : $"{mismatches} case(s) differ from expectation");
return mismatches == 0 ? 0 : 1;

static GeminiOptions Gemini(IConfiguration config)
{
    var o = config.GetSection("Gemini").Get<GeminiOptions>() ?? new GeminiOptions();
    o.ProjectId = config["Gcp:ProjectId"] ?? o.ProjectId;
    return o;
}
