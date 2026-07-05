using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// Everything the <see cref="AnnotatedChartComposer"/> needs to lay out a publication-grade chart:
/// the candles and price scale, the wave labels, the invalidation line, the entry/target zone boxes,
/// the projected channels and the scenario arrows, plus the title metadata. Optional collections
/// default to empty so a caller can populate only what it has (a saved analysis has no stored pivots,
/// so it omits labels/channels; a live projection supplies them). The render date is passed in — never
/// read from the clock — so the output stays deterministic.
/// </summary>
/// <param name="Symbol">Instrument symbol shown in the title, e.g. "BTC".</param>
/// <param name="Timeframe">Timeframe label shown in the title, e.g. "1D".</param>
/// <param name="RenderDate">The "as of" date stamped in the title (no clock read — determinism).</param>
/// <param name="Scale">Price axis scale; log maps prices through ln so a log channel is straight.</param>
/// <param name="Candles">The OHLC candles to draw (may be empty — a placeholder is drawn instead).</param>
public sealed record AnnotatedChartInput(
    string Symbol,
    string Timeframe,
    DateTime RenderDate,
    FibScale Scale,
    IReadOnlyList<MarketCandle> Candles)
{
    /// <summary>Canvas width in pixels.</summary>
    public int Width { get; init; } = 1200;

    /// <summary>Canvas height in pixels.</summary>
    public int Height { get; init; } = 700;

    /// <summary>Wave labels to place at their pivots (e.g. 1–5, A–C). Empty when pivots are unknown.</summary>
    public IReadOnlyList<WaveAnnotation> Labels { get; init; } = [];

    /// <summary>The hard invalidation line, or null if the count has none.</summary>
    public PriceLevel? Invalidation { get; init; }

    /// <summary>The entry (pullback) zone box, or null if none.</summary>
    public PriceZone? EntryZone { get; init; }

    /// <summary>Forward target zone box(es).</summary>
    public IReadOnlyList<PriceZone> TargetZones { get; init; } = [];

    /// <summary>Projected Elliott channels drawn as rays across the plot.</summary>
    public IReadOnlyList<Channel> Channels { get; init; } = [];

    /// <summary>Scenario arrows (primary solid, alternates dashed).</summary>
    public IReadOnlyList<ChartScenarioArrow> Scenarios { get; init; } = [];
}
