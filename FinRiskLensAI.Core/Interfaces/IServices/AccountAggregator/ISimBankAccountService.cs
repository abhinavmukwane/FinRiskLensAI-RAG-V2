namespace FinRiskLensAI.Core.Interfaces.IServices.AccountAggregator
{
    /// <summary>
    /// Registers a simulated bank account against a mobile number in the Finfactor
    /// SimBanks sandbox, so the Account Aggregator consent flow has something to
    /// discover for a newly onboarded MSME.
    /// </summary>
    public interface ISimBankAccountService
    {
        /// <summary>
        /// Creates a CURRENT / DEPOSIT account for <paramref name="mobileNumber"/>.
        /// Best-effort: returns false instead of throwing, so a sandbox outage can
        /// never block onboarding.
        /// </summary>
        Task<bool> RegisterAccountAsync(string? mobileNumber, string? email = null,
                                        CancellationToken ct = default);
    }
}
