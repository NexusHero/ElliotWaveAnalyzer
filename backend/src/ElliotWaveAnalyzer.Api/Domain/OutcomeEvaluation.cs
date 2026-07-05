namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Result of evaluating a saved analysis against subsequent candles: the outcome plus the
/// price and date that settled it (both null when no candles have formed since the save).
/// </summary>
public sealed record OutcomeEvaluation(
    AnalysisOutcome Outcome,
    decimal? Price,
    DateTimeOffset? At);
