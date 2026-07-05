namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One symbol the scanner flagged as having a setup: the best deterministic count found on it and
/// where current price sits relative to that count's geometry. All fields are computed — no LLM.
/// </summary>
/// <param name="Symbol">The instrument.</param>
/// <param name="Structure">Best count's structure, e.g. "Impulse".</param>
/// <param name="UnfoldingWave">The wave assumed to be in progress, e.g. "Wave 4".</param>
/// <param name="Bullish">Direction of the count.</param>
/// <param name="Score">Deterministic guideline score in [0, 1] (0 when unscored).</param>
/// <param name="CurrentPrice">Latest close.</param>
/// <param name="InvalidationPrice">The hard invalidation line, if any.</param>
/// <param name="DistanceToInvalidationPercent">Absolute distance from price to invalidation, in percent (null if no line).</param>
/// <param name="InEntryZone">True when price sits inside the expected support/entry zone.</param>
/// <param name="InConfluenceZone">True when price sits inside a scored confluence zone.</param>
public sealed record ScanHit(
    string Symbol,
    string Structure,
    string UnfoldingWave,
    bool Bullish,
    decimal Score,
    decimal CurrentPrice,
    decimal? InvalidationPrice,
    decimal? DistanceToInvalidationPercent,
    bool InEntryZone,
    bool InConfluenceZone);
