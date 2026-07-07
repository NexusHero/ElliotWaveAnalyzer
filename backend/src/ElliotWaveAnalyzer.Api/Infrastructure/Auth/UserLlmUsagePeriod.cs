namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// One user's LLM-call count for one quota period (#174) — a row only exists once a user has made
/// at least one call on the operator's shared key in that period. Persisted so the count survives a
/// restart (AC3) and resets naturally once <see cref="Application.QuotaPeriodCalculator"/> computes a
/// new period boundary (AC4) — no cleanup job needed for the reset itself.
/// </summary>
internal sealed class UserLlmUsagePeriod
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset PeriodStart { get; set; }

    public int CallCount { get; set; }
}
