namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Thrown when a user has exhausted their per-period LLM-call quota on the operator's shared key
/// (#174). Deliberately a dedicated type — caught once in <see cref="Infrastructure.GlobalExceptionHandler"/>
/// and mapped to a clear, actionable 429 response — rather than a generic failure. Never thrown for a
/// user calling on their own configured key (see <see cref="UserQuotaStatus"/>'s remarks).
/// </summary>
public sealed class LlmQuotaExceededException(UserQuotaStatus status) : Exception(
    $"You've used all {status.Limit} AI calls included for this period. It resets at " +
    $"{status.PeriodEnd:yyyy-MM-dd HH:mm} UTC. Deterministic features (rule checks, projections, " +
    "the scanner, risk sizing, live verify) remain fully available — only AI-narrated features are paused.")
{
    /// <summary>The exceeded quota's standing at the time of refusal.</summary>
    public UserQuotaStatus Status { get; } = status;
}
