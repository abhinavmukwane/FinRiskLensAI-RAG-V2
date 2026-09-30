using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Storage;

namespace FinRiskLensAI.Core.Interfaces
{
    /// <summary>Raw source payloads for one MSME, exactly as returned by the upstream APIs.</summary>
    public class SourceFactsRequest
    {
        public string? UdyamJson { get; set; }
        public string? ItrJson { get; set; }
        public string? AaJson { get; set; }
        public string? GstTaxpayerJson { get; set; }
        public List<string> Gstr3bJsons { get; set; } = new();          // one per month, up to 12
        public List<string> Gstr1SummaryJsons { get; set; } = new();    // one per month, up to 12
        public List<string> Gstr1B2bJsons { get; set; } = new();        // B2B / e-invoice payloads
        public List<string> Gstr1CdnrJsons { get; set; } = new();       // credit/debit notes
        public List<string> Gstr1HsnJsons { get; set; } = new();        // HSN summaries
        public List<string> Gstr2aB2bJsons { get; set; } = new();       // inward (purchase) invoices
        public List<string> Gstr2bJsons { get; set; } = new();          // auto-drafted ITC statements
        // EPFO payload slot — source arrives later, wired but unused for now
        public string? EpfoJson { get; set; }

        /// <summary>
        /// Routes a folder's files to their request slots by name. One implementation,
        /// shared by the blob service and the tests, so the mapping cannot drift.
        /// </summary>
        public static SourceFactsRequest FromFiles(IReadOnlyDictionary<string, string?> files)
        {
            string? Get(string name) => files.TryGetValue(name, out var v) ? v : null;

            var request = new SourceFactsRequest
            {
                UdyamJson = Get(MsmeDataFiles.Udyam),
                ItrJson = Get(MsmeDataFiles.Itr),
                AaJson = Get(MsmeDataFiles.Aa),
                GstTaxpayerJson = Get(MsmeDataFiles.GstTaxpayer),
                EpfoJson = Get(MsmeDataFiles.Epfo)
            };

            foreach (var file in files.Keys.OrderBy(f => f))   // same ordering the scorer used
            {
                var content = files[file] ?? string.Empty;
                if (file.StartsWith(MsmeDataFiles.Gstr3bPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr3bJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr1SummaryPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr1SummaryJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr1CdnrPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr1CdnrJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr1HsnPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr1HsnJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr2aB2bPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr2aB2bJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr2bPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr2bJsons.Add(content);
                else if (file.StartsWith(MsmeDataFiles.Gstr1B2bPrefix, StringComparison.OrdinalIgnoreCase))
                    request.Gstr1B2bJsons.Add(content);
            }
            return request;
        }
    }

    /// <summary>Turns raw source payloads into <see cref="SourceFacts"/>. Pure: no I/O, no model.</summary>
    public interface ISourceFactsService
    {
        SourceFacts Build(SourceFactsRequest request);
    }
}
