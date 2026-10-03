using System.Text.Json;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Data.DbContextEDMX;
using FinRiskLensAI.Data.Repositories.Cases;
using FinRiskLensAI.DemoData;
using FinRiskLensAI.Fraud.Cases;
using FinRiskLensAI.Fraud.Checks;
using FinRiskLensAI.ML.Storage;

// Demo data — Doc/10_FRAUD_LENS_BUILD.md, Phase 6. Run from the repository root.
//
//   generate <dir>   synthetic PDFs + <file>.truth.json (what an exact extractor reads) + cases.json
//   seed             the same cases through real intake → truth applied as extraction → checks run,
//                    then expected vs actual findings per case. Exit code 1 on any mismatch.

if (args.Length == 0 || args[0] is not ("generate" or "seed") || (args[0] == "generate" && args.Length < 2))
{
    Console.Error.WriteLine("usage: generate <dir> | seed");
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

// ── seed ──────────────────────────────────────────────────────────────────────────
var config = ApplicationDbContextFactory.BuildConfiguration();
var container = config["AzureBlob:Container"];
// V1's container holds live data under IDBI evaluation — this tool must never write there
if (string.IsNullOrWhiteSpace(container) || container == "msme-data")
{
    Console.Error.WriteLine($"Refusing to seed into blob container '{container}'. Set AzureBlob:Container to the fork's own container.");
    return 2;
}

await using var db = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>());
var repo = new CaseRepository(db);
var service = new CaseService(repo, new AzureBlobDataStore(config), new FraudOptions());
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
