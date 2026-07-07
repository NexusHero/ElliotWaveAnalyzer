namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The context overlay for a count (#188): upcoming catalysts near its projected turn dates, and how
/// correlated instruments are behaving relative to its thesis. Advisory only — nothing here can alter
/// the count, its levels, or its scenario tree (AC4); the report is assembled from separate calendar
/// and market-data facts and simply attaches alongside a count. Coverage is tracked per section
/// independently: a calendar provider may be configured with no correlated-instrument set, or vice
/// versa, and each says so honestly rather than a blanket "no context" hiding partial coverage (AC5).
/// </summary>
/// <param name="HasCatalystCoverage">False when no calendar/earnings provider was available.</param>
/// <param name="CatalystFlags">Catalysts falling within the window of a projected turn date.</param>
/// <param name="HasIntermarketCoverage">False when no correlated-instrument data was available.</param>
/// <param name="IntermarketSignals">Classified corroboration/contradiction readings.</param>
public sealed record ContextReport(
    bool HasCatalystCoverage,
    IReadOnlyList<CatalystFlag> CatalystFlags,
    bool HasIntermarketCoverage,
    IReadOnlyList<IntermarketSignal> IntermarketSignals)
{
    /// <summary>The fact-derived narrative, or null when unavailable (see <see cref="NarrativeUnavailableReason"/>).</summary>
    public string? Narrative { get; init; }

    /// <summary>Why the narrative is absent (no LLM key, failed fact-guard), or null when present.</summary>
    public string? NarrativeUnavailableReason { get; init; }

    /// <summary>The explicit no-coverage report (AC5) — never a fabricated catalyst or reading.</summary>
    public static ContextReport NoCoverage() => new(false, [], false, []);
}
