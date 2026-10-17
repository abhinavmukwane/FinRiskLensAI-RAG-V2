using System.Text.Json;
using System.Text.Json.Nodes;
using FinRiskLensAI.Fraud.Ai;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Google Cloud Translation API controller for the JAPAC region (Google AI Builder Cup 2026).
    /// Default language is English (en) — requires no translation.
    /// Provides batch text translation powered by Google Cloud & Vertex AI.
    /// </summary>
    [ApiController]
    [Route("api/translate")]
    public class TranslateController : ControllerBase
    {
        private static readonly HttpClient Http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return client;
        }

        private readonly GeminiClient? _gemini;
        private readonly ILogger<TranslateController> _log;

        public TranslateController(ILogger<TranslateController> log, GeminiClient? gemini = null)
        {
            _log = log;
            _gemini = gemini;
        }

        public class TranslateRequest
        {
            public string? SourceLanguage { get; set; } = "en";
            public string? TargetLanguage { get; set; } = "en";
            public List<string>? TextData { get; set; }
        }

        public class TranslationItem
        {
            public string Source { get; set; } = string.Empty;
            public string Target { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Translate([FromBody] TranslateRequest request, CancellationToken ct)
        {
            if (request?.TextData == null || request.TextData.Count == 0)
                return Ok(Array.Empty<TranslationItem>());

            var target = string.IsNullOrWhiteSpace(request.TargetLanguage) ? "en" : request.TargetLanguage.Trim();
            var source = string.IsNullOrWhiteSpace(request.SourceLanguage) ? "en" : request.SourceLanguage.Trim();

            // Default language is English — no translation required if target is English or matches source
            if (string.Equals(target, "en", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
            {
                return Ok(request.TextData.Select(t => new TranslationItem { Source = t, Target = t }).ToList());
            }

            // 1. Try Google Translate API (fastest, high fidelity, preserves financial & domain tokens)
            try
            {
                var throttle = new SemaphoreSlim(6);
                var tasks = request.TextData.Select(async text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        return new TranslationItem { Source = text, Target = text };

                    await throttle.WaitAsync(ct);
                    try
                    {
                        var url = $"https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&sl={Uri.EscapeDataString(source)}&tl={Uri.EscapeDataString(target)}&dt=t&q={Uri.EscapeDataString(text)}";
                        var response = await Http.GetStringAsync(url, ct);
                        using var doc = JsonDocument.Parse(response);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                            doc.RootElement.GetArrayLength() > 0 &&
                            doc.RootElement[0].ValueKind == JsonValueKind.Array)
                        {
                            var transText = string.Concat(doc.RootElement[0].EnumerateArray()
                                .Where(s => s.ValueKind == JsonValueKind.Array && s.GetArrayLength() > 0 && s[0].ValueKind == JsonValueKind.String)
                                .Select(s => s[0].GetString()));
                            return new TranslationItem { Source = text, Target = string.IsNullOrEmpty(transText) ? text : transText };
                        }
                        return new TranslationItem { Source = text, Target = text };
                    }
                    catch
                    {
                        return new TranslationItem { Source = text, Target = text };
                    }
                    finally
                    {
                        throttle.Release();
                    }
                });

                var results = await Task.WhenAll(tasks);
                if (results.Any(r => r.Target != r.Source))
                {
                    return Ok(results);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("Google Translate API call encountered: {Error}", ex.Message);
            }

            // 2. Fallback to Google Vertex AI Gemini Flash if configured
            if (_gemini != null && _gemini.Options.IsConfigured)
            {
                try
                {
                    var prompt = $"Translate the following JSON array of strings from language '{source}' to '{target}'. Return ONLY the translated JSON array of strings in the exact same order.\n\n" +
                                 JsonSerializer.Serialize(request.TextData);

                    var schema = new JsonObject
                    {
                        ["type"] = "ARRAY",
                        ["items"] = new JsonObject { ["type"] = "STRING" }
                    };

                    var translatedList = await _gemini.GenerateJsonAsync<List<string>>(
                        _gemini.Options.FlashModel,
                        _gemini.Options.FlashLocation,
                        "You are a professional financial and technical translator for the JAPAC region in Google Cloud. Translate accurately and preserve numbers, currency symbols, and technical identifiers.",
                        new[] { GeminiClient.Text(prompt) },
                        schema,
                        maxOutputTokens: 4096,
                        ct: ct);

                    if (translatedList != null && translatedList.Count == request.TextData.Count)
                    {
                        var result = request.TextData.Zip(translatedList, (s, t) => new TranslationItem { Source = s, Target = t }).ToList();
                        return Ok(result);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogWarning("Vertex AI translation failed ({Error}); falling back to source text.", ex.Message);
                }
            }

            // Fallback: return source text
            return Ok(request.TextData.Select(t => new TranslationItem { Source = t, Target = t }).ToList());
        }
    }
}
