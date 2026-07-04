namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// How a tracked analysis has played out since it was saved, decided by the first of these
/// events in the candles that followed. Serialized by name.
/// </summary>
public enum AnalysisOutcome
{
    /// <summary>Neither the invalidation nor the target has been touched yet — still unfolding.</summary>
    Pending,

    /// <summary>Price crossed the invalidation line: the count is void.</summary>
    Invalidated,

    /// <summary>Price entered the projected target zone before invalidating.</summary>
    TargetReached,
}

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

/// <summary>
/// Request to add an analysis to the caller's track record. The frontend fills this from a
/// ranked count and its forward levels.
/// </summary>
public sealed record TrackAnalysisRequest(
    string Symbol,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score);

/// <summary>
/// Result of evaluating a saved analysis against subsequent candles: the outcome plus the
/// price and date that settled it (both null when no candles have formed since the save).
/// </summary>
public sealed record OutcomeEvaluation(
    AnalysisOutcome Outcome,
    decimal? Price,
    DateTimeOffset? At);
