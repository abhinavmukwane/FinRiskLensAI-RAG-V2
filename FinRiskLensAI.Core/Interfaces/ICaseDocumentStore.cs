namespace FinRiskLensAI.Core.Interfaces
{
    /// <summary>
    /// Binary store for uploaded case documents. Paths are content-addressed
    /// (<c>cases/{sha256}{ext}</c>), so a write is idempotent and the same file reused
    /// across applications is stored once. Cloud Storage (GcsDocumentStore), bucket Gcs:CasesBucket.
    /// </summary>
    public interface ICaseDocumentStore
    {
        Task PutAsync(string path, byte[] content, string contentType, CancellationToken ct = default);
        Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default);
    }
}
