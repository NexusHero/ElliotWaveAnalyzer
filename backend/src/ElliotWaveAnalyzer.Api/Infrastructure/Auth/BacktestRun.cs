namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A persisted backtest run, keyed by its dataset hash so a re-run over the same candles + config is
/// idempotent (the hash is unique; we look it up before running again). Stores the run identity and
/// scenario count; the per-dimension hit-rate buckets hang off it as <see cref="BacktestBucketRow"/>s.
/// </summary>
internal sealed class BacktestRun
{
    public Guid Id { get; set; }

    /// <summary>Stable hash of the candle series + config + engine version. Unique — the run identity.</summary>
    public string DatasetHash { get; set; } = string.Empty;

    public string EngineVersion { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    /// <summary>Canonical config string, kept for reference/debugging.</summary>
    public string Config { get; set; } = string.Empty;

    public int ScenarioCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<BacktestBucketRow> Buckets { get; set; } = [];
}
