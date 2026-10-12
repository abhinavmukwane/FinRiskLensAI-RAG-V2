using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;

namespace FinRiskLensAI.Fraud.Ai
{
    /// <summary>
    /// Model names and where each is served — config, never code (Doc/11_RAG_ARCHITECTURE.md §16).
    /// Bound from "Gemini", with the project from "Gcp:ProjectId". Extraction and embeddings run
    /// in Mumbai so applicant documents stay in India; Pro is only served on the global endpoint
    /// and only ever sees extracted facts.
    /// </summary>
    public class GeminiOptions
    {
        public string ProjectId { get; set; } = string.Empty;
        public string ExtractModel { get; set; } = "gemini-3.5-flash";
        public string ExtractLocation { get; set; } = "asia-south1";
        /// <summary>Transcription needs little reasoning; thinking tokens also count against the output cap.</summary>
        public string ExtractThinkingLevel { get; set; } = "LOW";
        public string FlashModel { get; set; } = "gemini-3.8-flash";
        public string FlashLocation { get; set; } = "global";
        public string ProModel { get; set; } = "gemini-3.1-pro-preview";
        public string ProFallbackModel { get; set; } = "gemini-2.5-pro";
        public string ProLocation { get; set; } = "global";
        public string EmbeddingModel { get; set; } = "gemini-embedding-001";
        public string EmbeddingLocation { get; set; } = "asia-south1";
        public int EmbeddingDimensions { get; set; } = 768;

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ProjectId);
    }

    /// <summary>
    /// Vertex AI over REST — two endpoints, so no gRPC SDK. Credentials are Application Default:
    /// the developer's gcloud login locally, the Cloud Run service account in production.
    /// </summary>
    public class GeminiClient
    {
        // Deadlines are per attempt (below), so the client itself never times out
        private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
        private const int MaxAttempts = 4;
        // A call that has not answered in two minutes is treated like a 503: back off and try again
        private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMinutes(2);

        private readonly GeminiOptions _options;
        private readonly ILogger<GeminiClient> _log;
        private readonly Lazy<Task<GoogleCredential>> _credential = new(async () =>
            (await GoogleCredential.GetApplicationDefaultAsync()).CreateScoped("https://www.googleapis.com/auth/cloud-platform"));

        public GeminiClient(GeminiOptions options, ILogger<GeminiClient> log)
        {
            _options = options;
            _log = log;
        }

        public GeminiOptions Options => _options;

        public static JsonObject Text(string text) => new() { ["text"] = text };

        public static JsonObject Inline(byte[] data, string mimeType) => new()
        {
            ["inlineData"] = new JsonObject { ["mimeType"] = mimeType, ["data"] = Convert.ToBase64String(data) }
        };

        /// <summary>
        /// One structured-output call. The schema is Vertex's OpenAPI subset (upper-case types).
        /// Temperature 0: extraction and judgement should not vary between runs.
        /// </summary>
        public async Task<T> GenerateJsonAsync<T>(string model, string location, string? systemInstruction,
                                                  IEnumerable<JsonNode> parts, JsonObject responseSchema,
                                                  int maxOutputTokens, string? thinkingLevel = null,
                                                  CancellationToken ct = default)
        {
            var body = new JsonObject
            {
                ["contents"] = new JsonArray(new JsonObject { ["role"] = "user", ["parts"] = new JsonArray(parts.ToArray()) }),
                ["generationConfig"] = new JsonObject
                {
                    ["temperature"] = 0,
                    ["responseMimeType"] = "application/json",
                    ["responseSchema"] = responseSchema,
                    ["maxOutputTokens"] = maxOutputTokens
                }
            };
            if (thinkingLevel != null)
                body["generationConfig"]!["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = thinkingLevel };
            if (systemInstruction != null)
                body["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(Text(systemInstruction)) };

            using var response = await PostAsync(Endpoint(model, location, "generateContent"), body, ct);
            var candidate = response.RootElement.GetProperty("candidates")[0];
            var finish = candidate.TryGetProperty("finishReason", out var fr) ? fr.GetString() : null;

            // Thinking models return thought parts first; only the answer parts carry the JSON
            var json = string.Concat(candidate.GetProperty("content").GetProperty("parts").EnumerateArray()
                .Where(p => !(p.TryGetProperty("thought", out var t) && t.GetBoolean()))
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null));

            if (finish == "MAX_TOKENS")
                throw new GeminiException($"{model} ran out of output tokens ({maxOutputTokens}) — the document is too long for one call.");
            if (string.IsNullOrWhiteSpace(json))
                throw new GeminiException($"{model} returned no content (finish reason {finish ?? "none"}).");

            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new GeminiException($"{model} returned JSON that does not match the schema.");
        }

        /// <summary>
        /// A raw generateContent call for callers that build the whole request — the Investigator's
        /// function-calling loop. Returns the response root.
        /// </summary>
        public async Task<JsonElement> GenerateRawAsync(string model, string location, JsonObject body, CancellationToken ct = default)
        {
            using var response = await PostAsync(Endpoint(model, location, "generateContent"), body, ct);
            return response.RootElement.Clone();
        }

        /// <summary>
        /// Embeds each text at <see cref="GeminiOptions.EmbeddingDimensions"/>, normalised to unit
        /// length — truncated Gemini embeddings are not, and pgvector's cosine and inner-product
        /// orderings only agree on unit vectors. Task type matters: RETRIEVAL_DOCUMENT for chunks,
        /// RETRIEVAL_QUERY for questions.
        /// </summary>
        public async Task<float[][]> EmbedAsync(IReadOnlyList<string> texts, string taskType, CancellationToken ct = default)
        {
            var url = Endpoint(_options.EmbeddingModel, _options.EmbeddingLocation, "predict");
            var results = new float[texts.Count][];
            using var gate = new SemaphoreSlim(4);

            // ponytail: gemini-embedding-001 takes one instance per request — four in flight is plenty
            // for corpora of hundreds of chunks; a batch endpoint is the upgrade for thousands.
            await Task.WhenAll(texts.Select(async (text, i) =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var body = new JsonObject
                    {
                        ["instances"] = new JsonArray(new JsonObject { ["content"] = text, ["task_type"] = taskType }),
                        ["parameters"] = new JsonObject
                        {
                            ["outputDimensionality"] = _options.EmbeddingDimensions,
                            ["autoTruncate"] = true
                        }
                    };
                    using var response = await PostAsync(url, body, ct);
                    var values = response.RootElement.GetProperty("predictions")[0].GetProperty("embeddings").GetProperty("values")
                                         .EnumerateArray().Select(v => v.GetSingle()).ToArray();
                    var norm = MathF.Sqrt(values.Sum(v => v * v));
                    results[i] = norm == 0 ? values : values.Select(v => v / norm).ToArray();
                }
                finally { gate.Release(); }
            }));
            return results;
        }

        private string Endpoint(string model, string location, string verb)
        {
            if (!_options.IsConfigured) throw new GeminiException("Gcp:ProjectId is not configured.");
            var host = location == "global" ? "aiplatform.googleapis.com" : $"{location}-aiplatform.googleapis.com";
            return $"https://{host}/v1/projects/{_options.ProjectId}/locations/{location}/publishers/google/models/{model}:{verb}";
        }

        private async Task<JsonDocument> PostAsync(string url, JsonNode body, CancellationToken ct)
        {
            var credential = await _credential.Value;
            var payload = body.ToJsonString();

            for (int attempt = 1; ; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                var token = await ((ITokenAccess)credential).GetAccessTokenForRequestAsync(url, ct);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                // A user login bills its quota to the project set in the ADC file; the service account needs no header
                if (!string.IsNullOrEmpty(credential.QuotaProject))
                    request.Headers.Add("x-goog-user-project", credential.QuotaProject);

                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(AttemptTimeout);
                HttpResponseMessage response;
                string text;
                try
                {
                    response = await Http.SendAsync(request, deadline.Token);
                    text = await response.Content.ReadAsStringAsync(deadline.Token);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested && ex is OperationCanceledException or HttpRequestException)
                {
                    // Timed out or the connection dropped — transient, unless it keeps happening
                    if (attempt >= MaxAttempts)
                        throw new GeminiException($"Vertex AI did not answer after {MaxAttempts} attempts ({ex.GetType().Name}).");
                    _log.LogWarning("Vertex AI {Error} on attempt {Attempt}; retrying", ex.GetType().Name, attempt);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    continue;
                }
                using var _ = response;
                if (response.IsSuccessStatusCode) return JsonDocument.Parse(text);

                var transient = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
                                                    or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
                                                    or HttpStatusCode.GatewayTimeout;
                if (!transient || attempt == MaxAttempts)
                    throw new GeminiException($"Vertex AI {(int)response.StatusCode}: {Trim(text)}");

                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(250, 1000));
                _log.LogWarning("Vertex AI {Status} on attempt {Attempt}; retrying in {Delay}", (int)response.StatusCode, attempt, delay);
                await Task.Delay(delay, ct);
            }
        }

        private static string Trim(string s) => s.Length <= 400 ? s : s[..400] + "…";
    }

    public class GeminiException(string message) : Exception(message);
}
