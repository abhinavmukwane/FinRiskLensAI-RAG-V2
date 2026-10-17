using System.Text.Json;
using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Fraud.Ai;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// The FinRiskLens AI Assistant (Views/Shared/_Chatbot.cshtml). Answers questions about the
    /// signed-in business's whole assessment, whatever page is open: the context is rebuilt
    /// server-side from the session's own UAN on every question (<see cref="AssessmentContextBuilder"/>),
    /// so nothing a page or the browser sends can point it at another business. Gemini does the
    /// explaining (<see cref="CreditAssistant"/>); the rule-based analyses stay the source of truth.
    /// Conversation history lives in the session, keyed by UAN, a few short turns only.
    /// </summary>
    [CustDashboardAuthorize]
    public class ChatbotController : Controller
    {
        private const int MaxQuestionChars = 1000;
        private const int MaxStoredTurns = 8;           // 4 questions and answers
        private const int MaxStoredChars = 4000;        // per stored turn
        private const string HistoryPrefix = "FrlChat:History:";
        private const string LastAskPrefix = "FrlChat:LastAsk:";
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(2);

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        private readonly AssessmentContextBuilder _context;
        private readonly CreditAssistant _assistant;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(AssessmentContextBuilder context, CreditAssistant assistant, ILogger<ChatbotController> logger)
        {
            _context = context;
            _assistant = assistant;
            _logger = logger;
        }

        /// <summary>Opening state: this session's conversation and suggestions based on the available data.</summary>
        [HttpGet]
        public async Task<IActionResult> State(CancellationToken ct)
        {
            var session = CurrentSession();
            if (session == null) return Json(new { status = false, error = "Your session has expired. Sign in again." });

            var built = await _context.BuildAsync(session, ct);
            return Json(new
            {
                status = true,
                history = LoadHistory(session.Uan),
                suggestions = built.Suggestions,
                available = built.Available,
                missing = built.Missing
            });
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatbotAskViewModel? request, CancellationToken ct)
        {
            var session = CurrentSession();
            if (session == null) return Json(new { status = false, error = "Your session has expired. Sign in again." });

            var question = Clean(request?.Question);
            if (question.Length == 0)
                return Json(new { status = false, error = "Type a question first." });
            if (question.Length > MaxQuestionChars)
                return Json(new { status = false, error = $"Keep the question under {MaxQuestionChars} characters." });

            // One question at a time per session
            var lastKey = LastAskPrefix + session.Uan;
            if (long.TryParse(HttpContext.Session.GetString(lastKey), out var last)
                && DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) < MinInterval)
                return Json(new { status = false, error = "Please wait a moment before asking again.", retryable = true });
            HttpContext.Session.SetString(lastKey, DateTime.UtcNow.Ticks.ToString());

            var history = LoadHistory(session.Uan);
            try
            {
                var built = await _context.BuildAsync(session, ct);
                var turns = history.Select(h => new CreditAssistant.Turn(h.Role, h.Text)).ToList();
                var reply = await _assistant.AskAsync(built.Context, turns, question, Clean(request?.PageKey), ct);

                history.Add(new StoredTurn("user", question, null));
                history.Add(new StoredTurn("assistant", Trim(reply.Answer) ?? "", Trim(JsonSerializer.Serialize(reply, JsonOpts), MaxStoredChars)));
                SaveHistory(session.Uan, history);

                return Json(new { status = true, reply });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Assistant timed out for {Uan}", session.Uan);
                return Json(new { status = false, error = "The assistant took too long to answer. Try again.", retryable = true });
            }
            catch (GeminiException ex)
            {
                _logger.LogWarning(ex, "Assistant: Gemini error for {Uan}", session.Uan);
                return Json(new { status = false, error = "The assistant couldn't produce an answer. Try again.", retryable = true });
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
            {
                _logger.LogError(ex, "Assistant: AI service unavailable for {Uan}", session.Uan);
                return Json(new { status = false, error = "The AI service is unavailable right now. Try again in a moment.", retryable = true });
            }
        }

        /// <summary>Clears this business's conversation in this session.</summary>
        [HttpPost]
        public IActionResult Clear()
        {
            var session = CurrentSession();
            if (session != null) HttpContext.Session.Remove(HistoryPrefix + session.Uan);
            return Json(new { status = true });
        }

        // ── helpers ──

        public sealed record StoredTurn(string Role, string Text, string? Reply);

        private AssessmentContextBuilder.UserSession? CurrentSession()
        {
            var user = HttpContext.Session.GetCurrentUser();
            var uan = user?.UdyamNumber?.Trim();
            return string.IsNullOrWhiteSpace(uan) ? null
                : new AssessmentContextBuilder.UserSession(uan, user!.NameOfEnterprise, user.PanNumber, user.GstinNumber);
        }

        private List<StoredTurn> LoadHistory(string uan)
        {
            var raw = HttpContext.Session.GetString(HistoryPrefix + uan);
            if (string.IsNullOrWhiteSpace(raw)) return new();
            try { return JsonSerializer.Deserialize<List<StoredTurn>>(raw, JsonOpts) ?? new(); }
            catch (JsonException) { return new(); }
        }

        private void SaveHistory(string uan, List<StoredTurn> history) =>
            HttpContext.Session.SetString(HistoryPrefix + uan,
                JsonSerializer.Serialize(history.TakeLast(MaxStoredTurns).ToList(), JsonOpts));

        private static string Clean(string? s) =>
            new string((s ?? "").Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();

        private static string? Trim(string? s, int max = 1500) =>
            s == null ? null : s.Length <= max ? s : s[..max];
    }
}
