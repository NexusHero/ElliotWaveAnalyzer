namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A user's LLM-call quota standing for the current period (#174) — used calls against the operator's
/// shared key, the period's ceiling and reset time. A user calling on their <b>own</b> configured key
/// is never quota-limited (they cost the operator nothing); this status only ever reflects usage on
/// the operator's shared key.
/// </summary>
/// <param name="UsedCalls">LLM calls made on the operator's key so far this period.</param>
/// <param name="Limit">The period's call ceiling.</param>
/// <param name="PeriodStart">When the current period began (UTC).</param>
/// <param name="PeriodEnd">When the current period ends and the quota resets (UTC).</param>
public sealed record UserQuotaStatus(int UsedCalls, int Limit, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd)
{
    /// <summary>True when no further calls on the operator's key will be allowed this period.</summary>
    public bool IsExceeded => UsedCalls >= Limit;
}
