using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="FibConfluenceCalculator"/>: clustering of Fibonacci levels
/// into scored zones, degree weighting, outlier separation, and labelled contributions in both
/// scales. Legs are hand-built so the expected zones are computable by hand.
/// </summary>
[TestFixture]
public sealed class FibConfluenceCalculatorTests
{
    [Test]
    public void EntryZones_SingleLeg_WellSeparatedLevels_EachBecomeItsOwnZone()
    {
        // Leg 0→100 (linear): retracements at 61.8, 50, 38.2, 21.4 — all far apart at 1% tolerance.
        var zones = FibConfluenceCalculator.EntryZones(
            [new FibLeg(0m, 100m, "(1)→(2)", 1.0m)], FibScale.Linear, tolerancePercent: 1.0m);

        Assert.Multiple(() =>
        {
            Assert.That(zones, Has.Count.EqualTo(4));
            Assert.That(zones.All(z => z.Contributions.Count == 1), Is.True);
            Assert.That(zones.All(z => z.Score == 1.0m), Is.True);
            Assert.That(zones.All(z => z.Kind == ZoneKind.Entry), Is.True);
        });
    }

    [Test]
    public void EntryZones_CoincidentLevelsFromTwoDegrees_MergeAndSumWeights()
    {
        // Two identical legs at different degree weights → every ratio coincides → 4 zones,
        // each with two contributions and score 1 + 2 = 3.
        var zones = FibConfluenceCalculator.EntryZones(
            [new FibLeg(0m, 100m, "(1)→(2)", 1.0m), new FibLeg(0m, 100m, "[1]→[2]", 2.0m)],
            FibScale.Linear);

        Assert.Multiple(() =>
        {
            Assert.That(zones, Has.Count.EqualTo(4));
            Assert.That(zones.All(z => z.Contributions.Count == 2), Is.True);
            Assert.That(zones.All(z => z.Score == 3.0m), Is.True);
        });
    }

    [Test]
    public void EntryZones_NearLevelsFromTwoLegs_MergeIntoSpanningZones()
    {
        // Leg A 0→100 and Leg B 0→100.5: each ratio pair is within 1% (e.g. 50 & 50.25), so the
        // four ratios collapse to four two-contribution zones whose bounds span each pair.
        var zones = FibConfluenceCalculator.EntryZones(
            [new FibLeg(0m, 100m, "A", 1.0m), new FibLeg(0m, 100.5m, "B", 1.0m)],
            FibScale.Linear, tolerancePercent: 1.0m);

        Assert.Multiple(() =>
        {
            Assert.That(zones, Has.Count.EqualTo(4));
            Assert.That(zones.All(z => z.Contributions.Count == 2), Is.True);
            Assert.That(zones.All(z => z.Score == 2.0m), Is.True);
            Assert.That(zones.All(z => z.Low <= z.High), Is.True);
        });
    }

    [Test]
    public void EntryZones_LargerTolerance_MergesAcrossRatios()
    {
        // With a wide 20% band the 61.8% (38.2) and 50% (50) levels of one leg fall together,
        // proving density-based merging — a lone level would score less than this merged pair.
        var wide = FibConfluenceCalculator.EntryZones(
            [new FibLeg(0m, 100m, "(1)→(2)", 1.0m)], FibScale.Linear, tolerancePercent: 40m);

        Assert.That(wide[0].Contributions.Count, Is.GreaterThan(1));
        Assert.That(wide[0].Score, Is.GreaterThan(1.0m));
    }

    [Test]
    public void EntryZones_LabelsCarryRatioLegAndScale()
    {
        var log = FibConfluenceCalculator.EntryZones(
            [new FibLeg(10m, 100m, "(1)→(2)", 1.0m)], FibScale.Log);

        var contribution = log
            .SelectMany(z => z.Contributions)
            .First(c => c.Basis.StartsWith("61.8%", StringComparison.Ordinal));

        Assert.Multiple(() =>
        {
            Assert.That(contribution.Basis, Is.EqualTo("61.8% retracement of (1)→(2), log scale"));
            Assert.That((double)contribution.Price, Is.EqualTo(24.099).Within(0.01)); // log 61.8%
        });
    }

    [Test]
    public void TargetZones_ProjectExtensions_FromTheGivenPrice()
    {
        // Extensions of leg 50→100 from 100: 1.0×→150, 1.272×→163.6, 1.618×→180.9 (all separate).
        var zones = FibConfluenceCalculator.TargetZones(
            [new FibLeg(50m, 100m, "(1)", 1.0m)], projectFrom: 100m, FibScale.Linear);

        Assert.Multiple(() =>
        {
            Assert.That(zones, Has.Count.EqualTo(3));
            Assert.That(zones.All(z => z.Kind == ZoneKind.Target), Is.True);
            Assert.That(
                zones.SelectMany(z => z.Contributions).Any(c => c.Basis.Contains("1.618× extension")),
                Is.True);
        });
    }

    [Test]
    public void EntryZones_NoLegs_ReturnsEmpty()
        => Assert.That(FibConfluenceCalculator.EntryZones([], FibScale.Linear), Is.Empty);
}
