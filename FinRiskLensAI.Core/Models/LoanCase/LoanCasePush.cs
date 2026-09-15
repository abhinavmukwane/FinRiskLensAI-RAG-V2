using FinRiskLensAI.Core.Models.Common;

namespace FinRiskLensAI.Core.Models.LoanCase
{
    /// <summary>The downstream systems a scored MSME can be raised as a loan case in.</summary>
    public enum LoanCaseChannel
    {
        /// <summary>The bank's own Loan Origination System — a credit appraisal case.</summary>
        LOS,
        /// <summary>RBI's Unified Lending Interface (OCEN lineage) — a loan application with data packets.</summary>
        ULI,
        /// <summary>ONDC financial services (beckn protocol, domain ONDC:FIS12).</summary>
        ONDC
    }

    public enum LoanCasePushStatus
    {
        /// <summary>A real endpoint accepted it (2xx).</summary>
        Sent,
        /// <summary>No endpoint is configured for the channel; recorded locally with a generated reference.</summary>
        Simulated,
        /// <summary>The endpoint was called and refused, or was unreachable.</summary>
        Failed
    }

    /// <summary>
    /// One attempt to raise a scored MSME as a loan case downstream — append-only,
    /// never updated, never deleted.
    /// <para>
    /// Why append-only rather than one row per UAN: a case can legitimately be
    /// raised in LOS <i>and</i> ULI, and a re-push after a re-score is a real event
    /// a bank must be able to audit. The score, band and eligibility are frozen on
    /// the row because six months on you need to know what they were <i>when the
    /// case was raised</i>, not what they are now. "Already sent" is simply the
    /// latest row per (Uan, Channel).
    /// </para>
    /// </summary>
    public class LoanCasePush : AuditableEntity
    {
        public int LoanCasePushID { get; set; }

        public string Uan { get; set; } = string.Empty;
        /// <summary>Frozen at push time — an enterprise can be renamed; the case it raised was under this name.</summary>
        public string? EnterpriseName { get; set; }
        public LoanCaseChannel Channel { get; set; }
        public LoanCasePushStatus Status { get; set; }

        /// <summary>The downstream case id — returned by the endpoint, or generated when simulated.</summary>
        public string? CaseReference { get; set; }

        /// <summary>Where it was sent; null when simulated.</summary>
        public string? Endpoint { get; set; }
        public int? HttpStatus { get; set; }

        /// <summary>The exact JSON that went over the wire (or would have).</summary>
        public string Payload { get; set; } = string.Empty;
        public string? ResponseBody { get; set; }
        public string? ErrorMessage { get; set; }

        // ── frozen at push time
        public double ScoreAtPush { get; set; }
        public string BandAtPush { get; set; } = string.Empty;
        public decimal EligibilityAtPush { get; set; }
        public string? ModelVersion { get; set; }
        /// <summary>When the score being pushed was computed — lets a later viewer see how stale it was.</summary>
        public DateTime ScoreComputedAt { get; set; }

        // ── who
        public int PushedByLoginId { get; set; }
        public string PushedByUserId { get; set; } = string.Empty;
        public string? PushedByName { get; set; }
        public string? PushedFromIp { get; set; }

        public DateTime PushedAt { get; set; }
    }

    /// <summary>
    /// The latest push for one (Uan, Channel) — what the Customer 360 chip and the
    /// customers list show. Carries the current score so the view can flag a
    /// case whose score has moved since it was raised.
    /// </summary>
    public class LoanCasePushSummary
    {
        public int LoanCasePushID { get; set; }
        public LoanCaseChannel Channel { get; set; }
        public LoanCasePushStatus Status { get; set; }
        public string? CaseReference { get; set; }
        public double ScoreAtPush { get; set; }
        public string BandAtPush { get; set; } = string.Empty;
        public DateTime PushedAt { get; set; }
        public string? PushedByName { get; set; }

        /// <summary>True when the MSME has been re-scored since this push.</summary>
        public bool IsStale(double? currentScore, DateTime? currentComputedAt)
            => currentScore.HasValue
               && (Math.Abs(currentScore.Value - ScoreAtPush) >= 1
                   || (currentComputedAt.HasValue && currentComputedAt.Value > PushedAt));
    }

    /// <summary>Outcome of a push, returned to the UI.</summary>
    public class LoanCasePushResult
    {
        public bool Succeeded { get; set; }
        public int LoanCasePushID { get; set; }
        public LoanCaseChannel Channel { get; set; }
        public LoanCasePushStatus Status { get; set; }
        public string? CaseReference { get; set; }
        public string? Message { get; set; }
    }

    /// <summary>
    /// Bound from the "LoanCase" section of appsettings. A blank endpoint means the
    /// channel runs in Simulated mode — the payload is built and recorded exactly
    /// as it would be sent, but no HTTP call is made. Pointing a channel at a real
    /// URL is the whole integration change: no code is touched.
    /// </summary>
    public class LoanCaseSettings
    {
        public Dictionary<string, string?> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Optional bearer/API key sent as Authorization when an endpoint is configured.</summary>
        public Dictionary<string, string?> ApiKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        /// <summary>Identifies this lender in ULI / ONDC envelopes.</summary>
        public string LenderId { get; set; } = "IBKL";
        public string LenderName { get; set; } = "IDBI Bank";
        /// <summary>Subscriber id for beckn context (bap_id); dummy until ONDC registration.</summary>
        public string OndcSubscriberId { get; set; } = "finrisklensai.com";
        public string OndcSubscriberUri { get; set; } = "https://finrisklensai.com/ondc";

        public string? EndpointFor(LoanCaseChannel channel)
            => Endpoints.TryGetValue(channel.ToString(), out var url) && !string.IsNullOrWhiteSpace(url) ? url.Trim() : null;

        public string? ApiKeyFor(LoanCaseChannel channel)
            => ApiKeys.TryGetValue(channel.ToString(), out var key) && !string.IsNullOrWhiteSpace(key) ? key.Trim() : null;
    }
}
