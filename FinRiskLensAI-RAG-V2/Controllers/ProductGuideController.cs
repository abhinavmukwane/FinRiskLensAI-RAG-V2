using System.Text.Json;
using FinRiskLensAI.Common;
using FinRiskLensAI.Fraud.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// The FinRiskLens AI Product Guide on the public home page (Views/Home/_ProductGuide.cshtml). No sign-in:
    /// it explains the product from <see cref="ProductGuideKnowledge"/> and never touches customer data, so it
    /// is deliberately separate from the signed-in <see cref="ChatbotController"/>. Starter chips get curated
    /// answers; free questions go to Gemini (<see cref="ProductGuide"/>), with the closest curated answer as the
    /// fallback. The conversation lives in the session for this visit; questions carrying identifiers are
    /// answered without being stored or sent to Gemini.
    /// </summary>
    public class ProductGuideController : Controller
    {
        public const string RateLimitPolicy = "product-guide";

        private const int MaxQuestionChars = 500;
        private const int MaxStoredTurns = 12;          // 6 questions and answers
        private const int MaxContextTurns = 6;          // sent to Gemini
        private const int MaxQuestionsPerSession = 40;
        private const string HistoryKey = "FrlGuide:History";
        private const string LastAskKey = "FrlGuide:LastAsk";
        private const string CountKey = "FrlGuide:Count";
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        private readonly ProductGuide _guide;
        private readonly ILogger<ProductGuideController> _logger;

        public ProductGuideController(ProductGuide guide, ILogger<ProductGuideController> logger)
        {
            _guide = guide;
            _logger = logger;
        }

        /// <summary>Opening state: the welcome, the starter chips and this visit's conversation.</summary>
        [HttpGet]
        public IActionResult State()
        {
            _guide.WarmUp();   // readies Gemini while the visitor reads the welcome
            return Json(new
            {
                status = true,
                title = ProductGuideKnowledge.WelcomeTitle,
                welcome = ProductGuideKnowledge.Welcome,
                suggested = ProductGuideKnowledge.SuggestedQuestions,
                starters = ProductGuideKnowledge.Starters.Select(s => new { id = s.Id, label = s.Label, icon = s.Icon }),
                startRoute = ProductGuideKnowledge.StartRoute,
                history = LoadHistory()
            });
        }

        public sealed class AskRequest
        {
            public string? Question { get; set; }
            public string? Topic { get; set; }
        }

        [HttpPost]
        [EnableRateLimiting(RateLimitPolicy)]   // app-wide Gemini budget, see Program.cs
        public async Task<IActionResult> Ask([FromBody] AskRequest? request, CancellationToken ct)
        {
            var topic = ProductGuideKnowledge.FindTopic(request?.Topic);
            var question = topic?.Title ?? Clean(request?.Question);
            if (question.Length == 0)
                return Json(new { status = false, error = "Type a question first." });
            if (question.Length > MaxQuestionChars)
                return Json(new { status = false, error = $"Keep the question under {MaxQuestionChars} characters." });

            if (long.TryParse(HttpContext.Session.GetString(LastAskKey), out var last)
                && DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) < MinInterval)
                return Json(new { status = false, error = "Please wait a moment before asking again.", retryable = true });
            HttpContext.Session.SetString(LastAskKey, DateTime.UtcNow.Ticks.ToString());

            var count = HttpContext.Session.GetInt32(CountKey) ?? 0;
            if (count >= MaxQuestionsPerSession)
                return Json(new { status = false, error = "You've reached the question limit for this visit. Start an assessment, or come back later.", retryable = false });
            HttpContext.Session.SetInt32(CountKey, count + 1);

            // Identifiers never reach Gemini or the session
            if (topic == null && ProductGuide.ContainsPrivateData(question))
                return Json(new { status = true, reply = ProductGuide.PrivateDataReply(), stored = false });

            var history = LoadHistory();
            ProductGuide.Reply reply;
            if (topic != null)
            {
                reply = ProductGuide.FromTopic(topic);
            }
            else
            {
                var turns = history.TakeLast(MaxContextTurns).Select(h => new ProductGuide.Turn(h.Role, h.Text)).ToList();
                try
                {
                    reply = await _guide.AskAsync(turns, question, ct);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Product Guide: Gemini timed out");
                    reply = ProductGuide.Fallback(question);
                }
                catch (Exception ex) when (ex is GeminiException or HttpRequestException or JsonException or InvalidOperationException)
                {
                    _logger.LogWarning(ex, "Product Guide: Gemini unavailable, answering from the product overview");
                    reply = ProductGuide.Fallback(question);
                }
            }

            history.Add(new StoredTurn("user", question, null));
            history.Add(new StoredTurn("assistant", Trim(reply.Answer, 1200), JsonSerializer.Serialize(reply, JsonOpts)));
            SaveHistory(history);

            return Json(new { status = true, reply, stored = true });
        }

        /// <summary>Clears this visit's conversation.</summary>
        [HttpPost]
        public IActionResult Clear()
        {
            HttpContext.Session.Remove(HistoryKey);
            return Json(new { status = true });
        }

        // ── helpers ──

        public sealed record StoredTurn(string Role, string Text, string? Reply);

        private List<StoredTurn> LoadHistory()
        {
            var raw = HttpContext.Session.GetString(HistoryKey);
            if (string.IsNullOrWhiteSpace(raw)) return new();
            try { return JsonSerializer.Deserialize<List<StoredTurn>>(raw, JsonOpts) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private void SaveHistory(List<StoredTurn> history) =>
            HttpContext.Session.SetString(HistoryKey, JsonSerializer.Serialize(history.TakeLast(MaxStoredTurns).ToList(), JsonOpts));

        private static string Clean(string? s) =>
            new string((s ?? "").Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();

        private static string Trim(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
