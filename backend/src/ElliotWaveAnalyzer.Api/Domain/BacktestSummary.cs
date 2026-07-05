namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The persisted, read-back result of a backtest run: the identity that makes a run reproducible
/// (dataset hash + engine version), how many scenarios were scored, and the aggregated hit-rate
/// buckets. Two runs over the same candles and config share a <see cref="DatasetHash"/>, so a re-run
/// is idempotent (same run identity, no duplicate rows).
/// </summary>
/// <param name="DatasetHash">Stable hash of the candle series + config that produced this run.</param>
/// <param name="EngineVersion">Version of the backtest engine, so results across versions are distinguishable.</param>
/// <param name="Symbol">Instrument the backtest ran over.</param>
/// <param name="ScenarioCount">Total scenarios recorded across all cutoffs.</param>
/// <param name="CreatedAt">When the run was first persisted (UTC).</param>
/// <param name="Buckets">Aggregated hit-rate buckets across all dimensions.</param>
public sealed record BacktestSummary(
    string DatasetHash,
    string EngineVersion,
    string Symbol,
    int ScenarioCount,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BacktestBucket> Buckets);
