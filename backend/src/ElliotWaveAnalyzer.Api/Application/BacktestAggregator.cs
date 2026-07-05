using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Aggregates recorded backtest scenarios into hit-rate buckets across every reporting dimension
/// (structure, confidence, confluence, timeframe). Open (still-pending) scenarios are counted in
/// each bucket's total but excluded from the hit-rate denominator (<see cref="BacktestBucket.HitRate"/>
/// divides by concluded), so an unsettled scenario never distorts the measured rate. Pure and
/// deterministic — the same results yield byte-stable buckets (sorted by dimension then key).
/// </summary>
public static class BacktestAggregator
{
    /// <summary>Aggregates <paramref name="results"/> into buckets across all dimensions.</summary>
    public static IReadOnlyList<BacktestBucket> Aggregate(IReadOnlyList<BacktestScenarioResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var buckets = new List<BacktestBucket>();
        AddDimension(buckets, "structure", results, r => r.Structure);
        AddDimension(buckets, "confidence", results, r => r.ConfidenceBucket);
        AddDimension(buckets, "confluence", results, r => r.ConfluenceBucket);
        AddDimension(buckets, "timeframe", results, r => r.Timeframe);
        return buckets;
    }

    private static void AddDimension(
        List<BacktestBucket> buckets,
        string dimension,
        IReadOnlyList<BacktestScenarioResult> results,
        Func<BacktestScenarioResult, string> key)
    {
        var grouped = results
            .GroupBy(key, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var total = group.Count();
            var concluded = group.Count(r => r.Concluded);
            var targetReached = group.Count(r => r.Outcome == AnalysisOutcome.TargetReached);
            var invalidated = group.Count(r => r.Outcome == AnalysisOutcome.Invalidated);
            buckets.Add(new BacktestBucket(dimension, group.Key, total, concluded, targetReached, invalidated));
        }
    }
}
