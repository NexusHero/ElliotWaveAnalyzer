namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// An aggregated hit rate for one slice of a backtest — e.g. dimension "confidence", key "high". Open
/// (still-pending) scenarios count toward <see cref="Total"/> but not <see cref="Concluded"/>, and the
/// <see cref="HitRate"/> denominator is <see cref="Concluded"/> — so an unsettled scenario never
/// inflates or deflates the measured rate.
/// </summary>
/// <param name="Dimension">What the scenarios were bucketed by: "structure" / "confidence" / "confluence" / "timeframe".</param>
/// <param name="Key">The value within the dimension, e.g. "Impulse" or "high".</param>
/// <param name="Total">All scenarios in this bucket (including open ones).</param>
/// <param name="Concluded">Scenarios that settled (invalidated or reached target).</param>
/// <param name="TargetReached">Concluded scenarios that reached their target.</param>
/// <param name="Invalidated">Concluded scenarios that invalidated.</param>
public sealed record BacktestBucket(
    string Dimension,
    string Key,
    int Total,
    int Concluded,
    int TargetReached,
    int Invalidated)
{
    /// <summary>Target-reached ÷ concluded, or null when nothing has concluded in this bucket.</summary>
    public decimal? HitRate => Concluded == 0 ? null : (decimal)TargetReached / Concluded;
}
