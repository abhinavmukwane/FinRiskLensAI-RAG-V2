using System.Net;
using FinRiskLensAI.Core.Interfaces;
using Google;
using Google.Cloud.Storage.V1;

namespace FinRiskLensAI.Fraud.Storage
{
    /// <summary>
    /// Case documents in a private Cloud Storage bucket in Mumbai. Paths are content-addressed,
    /// so a write of bytes that are already there is skipped. Credentials are Application
    /// Default. The bucket has public access prevention on; files reach the browser only
    /// through the app's own bank-session-gated endpoint.
    /// </summary>
    public class GcsDocumentStore : ICaseDocumentStore
    {
        private readonly string _bucket;
        private readonly Lazy<Task<StorageClient>> _client = new(() => StorageClient.CreateAsync());

        public GcsDocumentStore(string bucket) => _bucket = bucket;

        public async Task PutAsync(string path, byte[] content, string contentType, CancellationToken ct = default)
        {
            var client = await _client.Value;
            try
            {
                await client.GetObjectAsync(_bucket, path, cancellationToken: ct);
                return;
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound) { }

            using var stream = new MemoryStream(content);
            await client.UploadObjectAsync(_bucket, path, contentType, stream, cancellationToken: ct);
        }

        public async Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
        {
            var client = await _client.Value;
            var buffer = new MemoryStream();
            try
            {
                await client.DownloadObjectAsync(_bucket, path, buffer, cancellationToken: ct);
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            buffer.Position = 0;
            return buffer;
        }
    }
}
