namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A saved analysis in a user's track record, with its outcome evaluated against the price
/// action since it was saved. The stored fields (structure, direction, invalidation, target)
/// are the deterministic geometry from the analysis; <see cref="Outcome"/> and the evaluation
/// fields are computed fresh on read.
/// </summary>
/// <param name="Id">Stable identifier of the saved analysis.</param>
/// <param name="Symbol">Instrument symbol, e.g. "BTC".</param>
/// <param name="CreatedAt">When the analysis was saved (UTC).</param>
/// <param name="Structure">Pattern kind, e.g. "Impulse" or "Zigzag".</param>
/// <param name="Bullish">True when the tracked count is bullish.</param>
/// <param name="InvalidationPrice">The hard invalidation line, if the count had one.</param>
/// <param name="InvalidationAbove">
/// True when the invalidation line sits above price (a move up voids the count); false when it
/// sits below (a move down voids it). Mirrors <see cref="LevelSide"/>.
/// </param>
/// <param name="TargetLow">Lower bound of the projected target zone, if any.</param>
/// <param name="TargetHigh">Upper bound of the projected target zone, if any.</param>
/// <param name="Confidence">The LLM's confidence at save time ("high"/"medium"/"low").</param>
/// <param name="Score">The deterministic guideline score at save time, if any.</param>
/// <param name="Outcome">Computed outcome against the candles since <see cref="CreatedAt"/>.</param>
/// <param name="EvaluatedPrice">Latest close used in the evaluation, if candles were available.</param>
/// <param name="EvaluatedAt">Date of the candle that settled the outcome (or the latest seen).</param>
public sealed record TrackedAnalysis(
    Guid Id,
    string Symbol,
    DateTimeOffset CreatedAt,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score,
    AnalysisOutcome Outcome,
    decimal? EvaluatedPrice,
    DateTimeOffset? EvaluatedAt);
