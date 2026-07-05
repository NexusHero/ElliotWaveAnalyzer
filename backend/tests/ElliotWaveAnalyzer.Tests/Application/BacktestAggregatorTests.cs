using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Aggregation over a hand-built result set: exact bucket rates, and open scenarios excluded from the
/// hit-rate denominator (they count toward total, never toward concluded).
/// </summary>
[TestFixture]
public sealed class BacktestAggregatorTests
{
    private static BacktestScenarioResult Result(string structure, string confidence, AnalysisOutcome outcome) =>
        new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), structure, "1D", confidence, "none", true, outcome);

    // 6 impulse scenarios: 3 target-reached, 2 invalidated, 1 still open.
    private static IReadOnlyList<BacktestScenarioResult> Sample() =>
    [
        Result("Impulse", "high", AnalysisOutcome.TargetReached),
        Result("Impulse", "high", AnalysisOutcome.TargetReached),
        Result("Impulse", "medium", AnalysisOutcome.TargetReached),
        Result("Impulse", "high", AnalysisOutcome.Invalidated),
        Result("Impulse", "low", AnalysisOutcome.Invalidated),
        Result("Impulse", "low", AnalysisOutcome.Pending),
    ];

    [Test]
    public void Aggregate_StructureBucket_CountsAndRate()
    {
        var buckets = BacktestAggregator.Aggregate(Sample());
        var impulse = buckets.Single(b => b is { Dimension: "structure", Key: "Impulse" });

        Assert.Multiple(() =>
        {
            Assert.That(impulse.Total, Is.EqualTo(6));
            Assert.That(impulse.Concluded, Is.EqualTo(5), "the open scenario is excluded from concluded");
            Assert.That(impulse.TargetReached, Is.EqualTo(3));
            Assert.That(impulse.Invalidated, Is.EqualTo(2));
            Assert.That(impulse.HitRate, Is.EqualTo(3m / 5m).Within(1e-9m));
        });
    }

    [Test]
    public void Aggregate_ConfidenceBuckets_SplitByKey()
    {
        var buckets = BacktestAggregator.Aggregate(Sample());

        var high = buckets.Single(b => b is { Dimension: "confidence", Key: "high" });
        var low = buckets.Single(b => b is { Dimension: "confidence", Key: "low" });

        Assert.Multiple(() =>
        {
            // high: 2 target-reached + 1 invalidated = 3 concluded, rate 2/3.
            Assert.That(high.Concluded, Is.EqualTo(3));
            Assert.That(high.HitRate, Is.EqualTo(2m / 3m).Within(1e-9m));
            // low: 1 invalidated + 1 open → 1 concluded, rate 0; open excluded from denominator.
            Assert.That(low.Total, Is.EqualTo(2));
            Assert.That(low.Concluded, Is.EqualTo(1));
            Assert.That(low.HitRate, Is.EqualTo(0m));
        });
    }

    [Test]
    public void Aggregate_AllOpenBucket_HasNullHitRate()
    {
        var buckets = BacktestAggregator.Aggregate([Result("Impulse", "high", AnalysisOutcome.Pending)]);
        var high = buckets.Single(b => b is { Dimension: "confidence", Key: "high" });

        Assert.That(high.HitRate, Is.Null);
    }

    [Test]
    public void Aggregate_IsDeterministic_SortedByDimensionThenKey()
        => Assert.That(BacktestAggregator.Aggregate(Sample()), Is.EqualTo(BacktestAggregator.Aggregate(Sample())));
}
