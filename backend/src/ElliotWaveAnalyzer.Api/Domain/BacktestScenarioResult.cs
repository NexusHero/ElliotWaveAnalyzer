namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One scenario recorded at a backtest cutoff and scored against the candles that followed. The
/// geometry (structure, direction, the confidence and confluence buckets) is decided using only the
/// candles up to the cutoff; the <see cref="Outcome"/> is the existing outcome semantics applied to
/// the following candles. <see cref="Concluded"/> is false while the scenario neither invalidated nor
/// reached target within the horizon — such rows are excluded from hit-rate denominators.
/// </summary>
/// <param name="CutoffDate">Time of the last candle visible when the scenario was recorded.</param>
/// <param name="Structure">Pattern kind, e.g. "Impulse".</param>
/// <param name="Timeframe">Timeframe label from the config.</param>
/// <param name="ConfidenceBucket">Score-derived confidence bucket: "high" / "medium" / "low".</param>
/// <param name="ConfluenceBucket">Top confluence-zone strength bucket: "strong" / "weak" / "none".</param>
/// <param name="Bullish">Direction of the recorded count.</param>
/// <param name="Outcome">Outcome scored over the horizon candles (Pending / Invalidated / TargetReached).</param>
public sealed record BacktestScenarioResult(
    DateTime CutoffDate,
    string Structure,
    string Timeframe,
    string ConfidenceBucket,
    string ConfluenceBucket,
    bool Bullish,
    AnalysisOutcome Outcome)
{
    /// <summary>True once the scenario settled (invalidated or reached target) within the horizon.</summary>
    public bool Concluded => Outcome != AnalysisOutcome.Pending;
}
