namespace FinRiskLensAI.Core.Models.Common
{
    /// <summary>
    /// Signzy API settings (appsettings.json "Signzy" section), bound once at
    /// startup like SmtpSettings/FinvuSettings. Never hardcode these in the
    /// service — always read them from configuration.
    /// </summary>
    public class SignzySettings
    {
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Raw Authorization header value (a plain token).</summary>
        public string Authorization { get; set; } = string.Empty;

        /// <summary>
        /// While false (no production key yet) the IP service serves the cached
        /// IPResponce from m_StaticResponces; flip to true to call the live API.
        /// </summary>
        public bool UseApi { get; set; } = false;
    }
}
