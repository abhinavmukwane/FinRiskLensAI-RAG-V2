using FinRiskLensAI.Common;
using FinRiskLensAI.Core.Interfaces.IServices.Common;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Utility;
using Microsoft.AspNetCore.Mvc;

namespace FinRiskLensAI.Controllers
{
    /// <summary>
    /// Universal in-app chatbot endpoint. Every dashboard page shares the same
    /// widget (Views/Shared/_Chatbot.cshtml); each page passes its own screen
    /// data once, on the first question, and this controller keeps it in the
    /// user's session for the rest of that page's conversation.
    /// </summary>
    [CustDashboardAuthorize]
    public class ChatbotController : Controller
    {
        private const int MaxHistoryTurns = 10;
        private const int MaxContextChars = 12000;
        private const string ContextSessionPrefix = "FrlChatCtx:";

        private readonly IChatbotService _chatbot;
        private readonly ILogger<ChatbotController> _logger;

        public ChatbotController(IChatbotService chatbot, ILogger<ChatbotController> logger)
        {
            _chatbot = chatbot;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatbotAskViewModel? request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request?.Question))
                return Json(new { status = false, answer = "Please type a question." });

            var pageKey = string.IsNullOrWhiteSpace(request.PageKey) ? "General" : request.PageKey.Trim();
            var sessionKey = ContextSessionPrefix + pageKey;

            // Screen data arrives only with the first question — cache it so
            // follow-up questions don't have to carry it again.
            if (!string.IsNullOrWhiteSpace(request.PageContext))
            {
                var ctx = request.PageContext.Length > MaxContextChars
                    ? request.PageContext[..MaxContextChars]
                    : request.PageContext;
                HttpContext.Session.SetString(sessionKey, ctx);
            }

            var screenContext = HttpContext.Session.GetString(sessionKey);

            // Who is asking — lets the bot address the client's own business.
            var user = HttpContext.Session.GetCurrentUser();
            if (user != null)
            {
                screenContext = $"Logged-in client: {user.NameOfEnterprise} (Udyam {user.UdyamNumber}).\n"
                              + (screenContext ?? string.Empty);
            }

            var history = (request.History ?? new List<ChatTurnModel>())
                .Where(h => !string.IsNullOrWhiteSpace(h.Content))
                .TakeLast(MaxHistoryTurns)
                .ToList();

            try
            {
                var answer = await _chatbot.AskAsync(screenContext, history, request.Question.Trim(), ct);
                return Json(new { status = true, answer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chatbot Ask failed for page {PageKey}", pageKey);
                return Json(new { status = false, answer = "Sorry — something went wrong. Please try again." });
            }
        }
    }
}
