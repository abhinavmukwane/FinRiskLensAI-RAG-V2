using FinRiskLensAI.Core.Models.Facts;
using FinRiskLensAI.Core.Models.Scoring;
using FinRiskLensAI.Core.Models.Storage;

namespace FinRiskLensAI.Core.Interfaces
{
    public class MsmeDataStatusReport
    {
        public string Uan { get; set; } = string.Empty;
        public MsmeDataManifest? Manifest { get; set; }
        public List<string> FilesPresent { get; set; } = new();
        public List<string> FilesMissing { get; set; } = new();
        public bool ReadyForAnalysis { get; set; }
    }

    public interface IBlobAnalysisService
    {
        Task SaveManifestAsync(string uan, MsmeDataManifest.ExpectedFiles expected, CancellationToken ct = default);
        Task<MsmeDataStatusReport> GetStatusAsync(string uan, CancellationToken ct = default);

        /// <summary>
        /// Builds the MSME's <see cref="SourceFacts"/> from its data folder, on demand.
        /// Nothing is written back — extraction is cheap now there is no model to run,
        /// and a fact computed per request can never be stale. Null when the folder is empty.
        /// </summary>
        Task<SourceFacts?> GetFactsAsync(string uan, CancellationToken ct = default);

        /// <summary>
        /// Evaluates the 6-dimension credit health score and lending assessment for the MSME.
        /// </summary>
        Task<RiskAnalysisResult?> GetResultAsync(string uan, CancellationToken ct = default);
    }
}
