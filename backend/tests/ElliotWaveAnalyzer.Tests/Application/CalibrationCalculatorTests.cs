using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="CalibrationCalculator"/>: bucketing by confidence, hit
/// rate over concluded analyses (pending excluded), ordering, and label normalization.
/// </summary>
[TestFixture]
public sealed class CalibrationCalculatorTests
{
    private static (string, AnalysisOutcome) A(string confidence, AnalysisOutcome outcome) => (confidence, outcome);

    [Test]
    public void Empty_ReturnsNoBucketsAndNullOverall()
    {
        var result = CalibrationCalculator.Calculate([]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Buckets, Is.Empty);
            Assert.That(result.TotalConcluded, Is.EqualTo(0));
            Assert.That(result.OverallHitRate, Is.Null);
        });
    }

    [Test]
    public void HitRate_IsTargetReachedOverConcluded_PendingExcluded()
    {
        var result = CalibrationCalculator.Calculate(
        [
            A("high", AnalysisOutcome.TargetReached),
            A("high", AnalysisOutcome.TargetReached),
            A("high", AnalysisOutcome.Invalidated),
            A("high", AnalysisOutcome.Pending), // counts to Total, not Concluded
        ]);

        var high = result.Buckets.Single();
        Assert.Multiple(() =>
        {
            Assert.That(high.Confidence, Is.EqualTo("high"));
            Assert.That(high.Total, Is.EqualTo(4));
            Assert.That(high.Concluded, Is.EqualTo(3));
            Assert.That(high.TargetReached, Is.EqualTo(2));
            Assert.That(high.Invalidated, Is.EqualTo(1));
            Assert.That(high.HitRate, Is.EqualTo(0.667m));
        });
    }

    [Test]
    public void AllPendingBucket_HasNullHitRate()
    {
        var result = CalibrationCalculator.Calculate([A("low", AnalysisOutcome.Pending)]);

        var low = result.Buckets.Single();
        Assert.Multiple(() =>
        {
            Assert.That(low.Total, Is.EqualTo(1));
            Assert.That(low.Concluded, Is.EqualTo(0));
            Assert.That(low.HitRate, Is.Null);
        });
    }

    [Test]
    public void Buckets_AreOrderedHighMediumLowThenOther()
    {
        var result = CalibrationCalculator.Calculate(
        [
            A("low", AnalysisOutcome.TargetReached),
            A("weird", AnalysisOutcome.TargetReached),
            A("high", AnalysisOutcome.TargetReached),
            A("medium", AnalysisOutcome.TargetReached),
        ]);

        Assert.That(
            result.Buckets.Select(b => b.Confidence),
            Is.EqualTo(new[] { "high", "medium", "low", "weird" }));
    }

    [Test]
    public void Confidence_IsNormalizedCaseInsensitively()
    {
        var result = CalibrationCalculator.Calculate(
        [
            A("High", AnalysisOutcome.TargetReached),
            A("HIGH", AnalysisOutcome.Invalidated),
            A(" high ", AnalysisOutcome.TargetReached),
        ]);

        var high = result.Buckets.Single();
        Assert.Multiple(() =>
        {
            Assert.That(high.Confidence, Is.EqualTo("high"));
            Assert.That(high.Total, Is.EqualTo(3));
            Assert.That(high.TargetReached, Is.EqualTo(2));
        });
    }

    [Test]
    public void BlankConfidence_BucketsAsUnknown()
    {
        var result = CalibrationCalculator.Calculate([A("  ", AnalysisOutcome.Invalidated)]);

        Assert.That(result.Buckets.Single().Confidence, Is.EqualTo("unknown"));
    }

    [Test]
    public void Overall_AggregatesAcrossBuckets()
    {
        var result = CalibrationCalculator.Calculate(
        [
            A("high", AnalysisOutcome.TargetReached),
            A("low", AnalysisOutcome.Invalidated),
            A("medium", AnalysisOutcome.TargetReached),
            A("low", AnalysisOutcome.Pending),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.TotalConcluded, Is.EqualTo(3)); // two targets + one invalidated
            Assert.That(result.OverallHitRate, Is.EqualTo(0.667m));
        });
    }

    [Test]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CalibrationCalculator.Calculate(null!));
    }
}
