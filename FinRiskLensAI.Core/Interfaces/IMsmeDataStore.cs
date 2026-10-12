namespace FinRiskLensAI.Core.Interfaces
{
    /// <summary>
    /// Storage abstraction for the per-MSME data folder (folder name = Udyam number).
    /// Cloud Storage (GcsMsmeDataStore), bucket Gcs:SourcesBucket; swappable for local-disk in tests.
    /// </summary>
    public interface IMsmeDataStore
    {
        Task UploadAsync(string uan, string fileName, string content, CancellationToken ct = default);
        Task<string?> DownloadAsync(string uan, string fileName, CancellationToken ct = default);
        Task<IReadOnlyList<string>> ListFilesAsync(string uan, CancellationToken ct = default);
        Task<bool> ExistsAsync(string uan, string fileName, CancellationToken ct = default);
    }
}
