namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Per-user LLM-call quota on the operator's shared key (#174). Bound from
/// <c>appsettings.json → LlmQuota</c>.
/// </summary>
internal sealed class LlmQuotaOptions
{
    public const string SectionName = "LlmQuota";

    /// <summary>Calls a single user may make on the operator's shared key per period.</summary>
    public int MaxCallsPerPeriod { get; init; } = 50;

    /// <summary>Length of a quota period, in days.</summary>
    public int PeriodDays { get; init; } = 1;
}
