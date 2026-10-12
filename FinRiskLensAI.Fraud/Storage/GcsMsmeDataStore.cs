using System.Net;
using System.Text;
using FinRiskLensAI.Core.Interfaces;
using Google;
using Google.Cloud.Storage.V1;

namespace FinRiskLensAI.Fraud.Storage
{
    /// <summary>
    /// The verification sources — Udyam, GST, ITR and Account Aggregator payloads — in a private
    /// Cloud Storage bucket in Mumbai. One virtual folder per MSME, named by its Udyam number,
    /// one JSON object per source file: the same layout the Azure store used, so the move was a copy.
    /// </summary>
    public class GcsMsmeDataStore : IMsmeDataStore
    {
        private readonly string _bucket;
        private readonly Lazy<Task<StorageClient>> _client = new(() => StorageClient.CreateAsync());

        public GcsMsmeDataStore(string bucket) => _bucket = bucket;

        /// <summary>UANs are used as folder names — keep them path-safe.</summary>
        private static string Folder(string uan) => uan.Trim().Replace('\\', '-').Replace('/', '-').ToUpperInvariant();

        private static string ObjectName(string uan, string fileName) => $"{Folder(uan)}/{fileName}";

        public async Task UploadAsync(string uan, string fileName, string content, CancellationToken ct = default)
        {
            var client = await _client.Value;
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            await client.UploadObjectAsync(_bucket, ObjectName(uan, fileName), "application/json", stream, cancellationToken: ct);
        }

        public async Task<string?> DownloadAsync(string uan, string fileName, CancellationToken ct = default)
        {
            var client = await _client.Value;
            using var buffer = new MemoryStream();
            try
            {
                await client.DownloadObjectAsync(_bucket, ObjectName(uan, fileName), buffer, cancellationToken: ct);
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        public async Task<IReadOnlyList<string>> ListFilesAsync(string uan, CancellationToken ct = default)
        {
            var client = await _client.Value;
            var prefix = Folder(uan) + "/";
            var names = new List<string>();
            await foreach (var obj in client.ListObjectsAsync(_bucket, prefix).WithCancellation(ct))
                names.Add(obj.Name[prefix.Length..]);
            return names;
        }

        public async Task<bool> ExistsAsync(string uan, string fileName, CancellationToken ct = default)
        {
            var client = await _client.Value;
            try
            {
                await client.GetObjectAsync(_bucket, ObjectName(uan, fileName), cancellationToken: ct);
                return true;
            }
            catch (GoogleApiException e) when (e.HttpStatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }
    }
}
