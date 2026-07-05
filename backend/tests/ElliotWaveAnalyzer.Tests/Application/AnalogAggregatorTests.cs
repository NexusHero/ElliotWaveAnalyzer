using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Aggregation: hit-rate and the target/invalidated split computed <b>only</b> from concluded analogs
/// (AC2), the median resolution time, and the "insufficient history" flag below the minimum (AC6).
/// </summary>
[TestFixture]
public sealed class AnalogAggregatorTests
{
    private static readonly SetupFeatures AnyFeatures =
        new(StructureKind.Impulse, true, "1d", 0.7, 0.5, 2.0, 0.08, 0.55, 0.6);

    private static HistoricalAnalog Analog(AnalysisOutcome outcome, double resolutionDays, double similarity = 0.9)
    {
        var formed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var concluded = outcome == AnalysisOutcome.Pending
            ? (DateTimeOffset?)null
            : formed.AddDays(resolutionDays);
        return new HistoricalAnalog(new HistoricalSetup("SYM", formed, concluded, outcome, AnyFeatures), similarity);
    }

    [Test]
    public void Aggregate_HitRate_ExcludesPendingFromDenominator()
    {
        // A defensive check: even if a pending analog slips in, it never enters the denominator.
        var analogs = new[]
        {
            Analog(AnalysisOutcome.TargetReached, 10),
            Analog(AnalysisOutcome.TargetReached, 14),
            Analog(AnalysisOutcome.TargetReached, 8),
            Analog(AnalysisOutcome.Invalidated, 6),
            Analog(AnalysisOutcome.Invalidated, 5),
            Analog(AnalysisOutcome.Pending, 0),
        };

        var stats = AnalogAggregator.Aggregate(analogs);

        Assert.Multiple(() =>
        {
            Assert.That(stats.SampleCount, Is.EqualTo(5)); // pending excluded
            Assert.That(stats.TargetReached, Is.EqualTo(3));
            Assert.That(stats.Invalidated, Is.EqualTo(2));
            Assert.That(stats.HitRate, Is.EqualTo(0.6).Within(1e-9));
        });
    }

    [Test]
    public void Aggregate_MedianResolutionDays_OddCount_IsMiddleValue()
    {
        var analogs = new[]
        {
            Analog(AnalysisOutcome.TargetReached, 5),
            Analog(AnalysisOutcome.TargetReached, 20),
            Analog(AnalysisOutcome.Invalidated, 10),
        };

        Assert.That(AnalogAggregator.Aggregate(analogs).MedianResolutionDays, Is.EqualTo(10).Within(1e-9));
    }

    [Test]
    public void Aggregate_MedianResolutionDays_EvenCount_IsMeanOfMiddlePair()
    {
        var analogs = new[]
        {
            Analog(AnalysisOutcome.TargetReached, 4),
            Analog(AnalysisOutcome.TargetReached, 8),
            Analog(AnalysisOutcome.Invalidated, 12),
            Analog(AnalysisOutcome.Invalidated, 16),
        };

        Assert.That(AnalogAggregator.Aggregate(analogs).MedianResolutionDays, Is.EqualTo(10).Within(1e-9));
    }

    [Test]
    public void Aggregate_BelowMinimumSample_IsInsufficient()
    {
        var analogs = new[]
        {
            Analog(AnalysisOutcome.TargetReached, 10),
            Analog(AnalysisOutcome.Invalidated, 6),
        };

        var stats = AnalogAggregator.Aggregate(analogs, minimumSample: 5);

        Assert.Multiple(() =>
        {
            Assert.That(stats.Sufficient, Is.False);
            Assert.That(stats.SampleCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void Aggregate_AtMinimumSample_IsSufficient()
    {
        var analogs = Enumerable.Range(0, 5)
            .Select(i => Analog(AnalysisOutcome.TargetReached, 10 + i))
            .ToArray();

        Assert.That(AnalogAggregator.Aggregate(analogs, minimumSample: 5).Sufficient, Is.True);
    }

    [Test]
    public void Aggregate_Empty_HasNullRatesAndIsInsufficient()
    {
        var stats = AnalogAggregator.Aggregate([]);

        Assert.Multiple(() =>
        {
            Assert.That(stats.SampleCount, Is.Zero);
            Assert.That(stats.HitRate, Is.Null);
            Assert.That(stats.MedianResolutionDays, Is.Null);
            Assert.That(stats.Sufficient, Is.False);
        });
    }
}
