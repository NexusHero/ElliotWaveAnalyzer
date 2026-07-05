namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>One aggregated hit-rate bucket of a <see cref="BacktestRun"/> (mirrors a BacktestBucket).</summary>
internal sealed class BacktestBucketRow
{
    public Guid Id { get; set; }

    public Guid BacktestRunId { get; set; }

    /// <summary>What the scenarios were bucketed by: "structure" / "confidence" / "confluence" / "timeframe".</summary>
    public string Dimension { get; set; } = string.Empty;

    /// <summary>The value within the dimension, e.g. "Impulse" or "high".</summary>
    public string Key { get; set; } = string.Empty;

    public int Total { get; set; }

    public int Concluded { get; set; }

    public int TargetReached { get; set; }

    public int Invalidated { get; set; }
}
