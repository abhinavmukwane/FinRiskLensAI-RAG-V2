namespace FinRiskLensAI.Core.Models.AccountAggregator
{
    public class AAConsentCallbackViewModel
    {
        public bool IsSuccess { get; set; }
        public string? ApplicationId { get; set; }
        public string? ConsentHandle { get; set; }
        public string? RawStatus { get; set; }
    }
}
