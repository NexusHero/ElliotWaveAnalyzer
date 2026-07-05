namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Full response of <c>POST /api/wave-analysis/auto</c>: every candidate count the system
/// found and ranked, an overall market summary, and the token usage of the LLM call.
/// <see cref="Rankings"/> is empty when no rule-valid wave structure could be detected.
/// </summary>
public sealed record AutoWaveAnalysisResponse(
    IReadOnlyList<RankedWaveCount> Rankings,
    string MarketSummary,
    TokenUsage Usage)
{
    /// <summary>
    /// True when the parser's evaluation budget truncated the search — the rankings are
    /// valid but coverage was bounded (never silently dropped).
    /// </summary>
    public bool SearchTruncated { get; init; }
}
