namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The single source of truth for an auto trade-thesis report (#187): every quantitative input the
/// engine already computes for a count, assembled into one record. An LLM narrative is written
/// <b>strictly from these facts</b> and passed through <see cref="Application.ThesisFactGuard"/> — it
/// cannot introduce a number that isn't here (ADR-009, ADR-028). Optional sections (<see cref="Risk"/>,
/// <see cref="CalibratedProbability"/>, <see cref="Analogs"/>) are null when the underlying engine
/// couldn't compute them (e.g. no valid stop, no calibration sample, no analog coverage) — never
/// defaulted to a fabricated value.
/// </summary>
/// <param name="Symbol">The instrument the thesis is for.</param>
/// <param name="ChainSummary">The top-down chain summary, e.g. "1W: Impulse → 1D: Zigzag [Consistent]".</param>
/// <param name="Bullish">Direction of the analyzed count.</param>
/// <param name="CurrentPrice">Latest close, or null when unavailable.</param>
/// <param name="Invalidation">The hard invalidation line, or null if undeterminable.</param>
/// <param name="EntryZone">The pullback/entry zone, or null if none.</param>
/// <param name="TargetZones">Forward target zone(s).</param>
/// <param name="Scale">Price scale the levels were computed on.</param>
/// <param name="Risk">R:R and suggested sizing, or null when there is no valid stop.</param>
/// <param name="ConfluenceZones">Fibonacci confluence zones behind the entry/target reads.</param>
/// <param name="CalibratedProbability">The measured hit-rate for this confidence, or null when the
/// track-record sample is too thin to publish a number.</param>
/// <param name="Analogs">Historical-analog resolution stats (#182), or null with no coverage.</param>
/// <param name="SentimentDivergences">Detected mood-vs-wave-position divergences (#183).</param>
/// <param name="Scenarios">The scenario tree (primary + alternates) behind this thesis.</param>
/// <param name="AsOf">When this fact sheet was assembled — supplied by the caller for reproducibility.</param>
public sealed record ThesisFactSheet(
    string Symbol,
    string ChainSummary,
    bool Bullish,
    decimal? CurrentPrice,
    PriceLevel? Invalidation,
    PriceZone? EntryZone,
    IReadOnlyList<PriceZone> TargetZones,
    FibScale Scale,
    RiskAssessment? Risk,
    IReadOnlyList<ConfluenceZone> ConfluenceZones,
    decimal? CalibratedProbability,
    AnalogStats? Analogs,
    IReadOnlyList<MoodDivergence> SentimentDivergences,
    IReadOnlyList<Scenario> Scenarios,
    DateTimeOffset AsOf)
{
    /// <summary>The disclaimer every thesis report must carry, verbatim (AC5).</summary>
    public const string Disclaimer = "Structural analysis of price action — not investment advice.";
}
