using FinRiskLensAI.Core.Interfaces;
using System.Text;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;

namespace FinRiskLensAI.ML.Storage
{
    /// <summary>
    /// Azure Blob Storage implementation. Each MSME gets a virtual folder named by its
    /// Udyam number inside one container; blobs are the raw source JSON files.
    /// Uploaded case documents share the container under <c>cases/</c>.
    /// </summary>
    public class AzureBlobDataStore : IMsmeDataStore, ICaseDocumentStore
    {
        private readonly Lazy<BlobContainerClient> _container;

        public AzureBlobDataStore(IConfiguration configuration)
        {
            var connectionString = configuration["AzureBlob:ConnectionString"]
                ?? throw new InvalidOperationException("Missing AzureBlob:ConnectionString in configuration.");
            var containerName = configuration["AzureBlob:Container"] ?? "msme-data";

            // Resilience: retry transient failures and give each try a generous network
            // timeout, so a brief thread-pool/CPU stall (e.g. during ML warmup) doesn't
            // surface as a TaskCanceledException on the request path.
            var options = new BlobClientOptions
            {
                Retry =
                {
                    MaxRetries = 3,
                    Mode = RetryMode.Exponential,
                    Delay = TimeSpan.FromMilliseconds(500),
                    MaxDelay = TimeSpan.FromSeconds(10),
                    NetworkTimeout = TimeSpan.FromSeconds(60)
                }
            };

            _container = new Lazy<BlobContainerClient>(() =>
            {
                var client = new BlobContainerClient(connectionString, containerName, options);
                client.CreateIfNotExists();
                return client;
            });
        }

        private static string BlobPath(string uan, string fileName) => $"{Sanitize(uan)}/{fileName}";

        /// <summary>UANs are used as folder names — keep them path-safe.</summary>
        private static string Sanitize(string uan)
            => uan.Trim().Replace('\\', '-').Replace('/', '-').ToUpperInvariant();

        public async Task UploadAsync(string uan, string fileName, string content, CancellationToken ct = default)
        {
            var blob = _container.Value.GetBlobClient(BlobPath(uan, fileName));
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await blob.UploadAsync(stream, overwrite: true, ct);
        }

        public async Task<string?> DownloadAsync(string uan, string fileName, CancellationToken ct = default)
        {
            var blob = _container.Value.GetBlobClient(BlobPath(uan, fileName));
            if (!await blob.ExistsAsync(ct)) return null;
            var response = await blob.DownloadContentAsync(ct);
            return response.Value.Content.ToString();
        }

        public async Task<IReadOnlyList<string>> ListFilesAsync(string uan, CancellationToken ct = default)
        {
            var prefix = Sanitize(uan) + "/";
            var names = new List<string>();
            await foreach (var blob in _container.Value.GetBlobsAsync(prefix: prefix, cancellationToken: ct))
                names.Add(blob.Name[prefix.Length..]);
            return names;
        }

        public async Task<bool> ExistsAsync(string uan, string fileName, CancellationToken ct = default)
            => await _container.Value.GetBlobClient(BlobPath(uan, fileName)).ExistsAsync(ct);

        public async Task PutAsync(string path, byte[] content, string contentType, CancellationToken ct = default)
        {
            var blob = _container.Value.GetBlobClient(path);
            // Content-addressed: if the bytes are already there, they are these bytes.
            if (await blob.ExistsAsync(ct)) return;
            await blob.UploadAsync(new BinaryData(content),
                new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct);
        }

        public async Task<Stream?> OpenReadAsync(string path, CancellationToken ct = default)
        {
            var blob = _container.Value.GetBlobClient(path);
            if (!await blob.ExistsAsync(ct)) return null;
            return await blob.OpenReadAsync(cancellationToken: ct);
        }
    }
}
