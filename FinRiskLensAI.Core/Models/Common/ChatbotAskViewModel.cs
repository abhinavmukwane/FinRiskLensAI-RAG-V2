
namespace FinRiskLensAI.Core.Models.Common
{
    /// <summary>
    /// One chatbot question from the browser. PageContext (the full screen
    /// data) is only populated on the FIRST question of a page's chat — the
    /// controller caches it in session for the rest of the conversation.
    /// </summary>
    public class ChatbotAskViewModel
    {
        public string? PageKey { get; set; }
        public string? PageContext { get; set; }
        public string? Question { get; set; }
        public List<ChatTurnModel>? History { get; set; }
    }
}
