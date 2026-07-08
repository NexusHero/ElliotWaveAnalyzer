namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Full response of <c>POST /api/wave-analysis/persona-panel</c> (#184): every candidate the
/// panel found, merged across personas and weighted by their measured track record, plus the
/// consensus score (the fraction of total persona weight behind the winning candidate — 1.0 for
/// unanimous agreement, lower when personas genuinely split) and each persona's own weight.
/// </summary>
public sealed record PersonaPanelResponse(
    IReadOnlyList<PersonaRankedCount> Rankings,
    IReadOnlyList<PersonaWeight> Weights,
    double ConsensusScore,
    string MarketSummary,
    TokenUsage Usage)
{
    /// <summary>True when the parser's evaluation budget truncated the candidate search.</summary>
    public bool SearchTruncated { get; init; }

    /// <summary>
    /// How many of <see cref="Domain.PersonaCatalog"/>'s personas actually ran (AC5): fewer than
    /// the full roster means the panel degraded under quota/budget pressure rather than failing
    /// outright — surfaced honestly rather than presented as a full panel.
    /// </summary>
    public int PersonasAttempted { get; init; }
}
