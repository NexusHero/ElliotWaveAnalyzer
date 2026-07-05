namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A per-position portfolio brief: the resolved instrument, its deterministic top-down count chain
/// and scenario geometry (invalidation, entry and target zones), where current price sits relative to
/// them, and an optional LLM narrative <b>derived strictly from these facts</b> (null when no key is
/// configured or the narrative failed the fact-guard — never a silent gap). All numbers are computed;
/// the LLM only narrates (ADR-009).
/// </summary>
/// <param name="Isin">The depot position's ISIN.</param>
/// <param name="Symbol">Data-source ticker the ISIN resolved to.</param>
/// <param name="Name">Human-readable instrument name.</param>
/// <param name="ChainSummary">The top-down chain, e.g. "1W: Impulse → 1D: Zigzag [Consistent]".</param>
/// <param name="Bullish">Direction of the finest-timeframe count.</param>
/// <param name="CurrentPrice">Latest close, or null when no candles were available.</param>
/// <param name="Invalidation">The hard invalidation line, or null if undeterminable.</param>
/// <param name="EntryZone">The pullback/entry zone, or null if none.</param>
/// <param name="TargetZones">Forward target zone(s).</param>
/// <param name="Scale">Price scale the levels were computed on.</param>
public sealed record PositionBrief(
    string Isin,
    string Symbol,
    string Name,
    string ChainSummary,
    bool Bullish,
    decimal? CurrentPrice,
    PriceLevel? Invalidation,
    PriceZone? EntryZone,
    IReadOnlyList<PriceZone> TargetZones,
    FibScale Scale)
{
    /// <summary>True when current price is above the invalidation line (a portfolio-level risk read).</summary>
    public bool AboveInvalidation { get; init; }

    /// <summary>True when current price sits inside the entry (pullback) zone.</summary>
    public bool InEntryZone { get; init; }

    /// <summary>The fact-derived narrative, or null when unavailable (see <see cref="NarrativeUnavailableReason"/>).</summary>
    public string? Narrative { get; init; }

    /// <summary>Why the narrative is absent (no LLM key, failed fact-guard), or null when present.</summary>
    public string? NarrativeUnavailableReason { get; init; }
}
