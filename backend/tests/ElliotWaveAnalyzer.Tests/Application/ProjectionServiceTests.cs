using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="ProjectionService"/>: invalidation lines, Fibonacci support and
/// target zones for the unfolding wave, across partial and complete counts, bull and bear.
/// Pivots are read as P0(origin), P1.. where P[i] ends wave i (see ElliottRuleChecker).
/// </summary>
[TestFixture]
public sealed class ProjectionServiceTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<WaveAnnotation> Pivots(params decimal[] prices)
        => [.. prices.Select((p, i) => new WaveAnnotation(Start.AddDays(i), p, "1"))];

    [Test]
    public void FewerThanTwoPivots_ReturnsNull()
    {
        Assert.That(ProjectionService.Project(Pivots(100m)), Is.Null);
    }

    [Test]
    public void Wave2_InvalidationIsOrigin_BelowForBull()
    {
        var levels = ProjectionService.Project(Pivots(100m, 120m))!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave 2"));
            Assert.That(levels.Bullish, Is.True);
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(100m));
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Below));
            // 50–61.8% retrace of wave 1 (leg 20): 110 and 107.64
            Assert.That(levels.SupportZone!.High, Is.EqualTo(110m).Within(0.01m));
            Assert.That(levels.SupportZone.Low, Is.EqualTo(107.64m).Within(0.01m));
            Assert.That(levels.Alternative, Is.Not.Null);
        });
    }

    [Test]
    public void Wave3_InvalidationIsWave2Start_TargetIsExtension()
    {
        var levels = ProjectionService.Project(Pivots(100m, 120m, 110m))!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave 3"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(110m));
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Below));
            Assert.That(levels.TargetZones, Has.Count.EqualTo(1));
            // 1.0–1.618× wave 1 (20) projected from 110: 130 and 142.36
            Assert.That(levels.TargetZones[0].Low, Is.EqualTo(130m).Within(0.01m));
            Assert.That(levels.TargetZones[0].High, Is.EqualTo(142.36m).Within(0.01m));
        });
    }

    [Test]
    public void Wave4_InvalidationIsWave1End_TheClassicLine()
    {
        var levels = ProjectionService.Project(Pivots(100m, 120m, 110m, 150m))!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave 4"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(120m)); // end of wave 1
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Below));
            // 23.6–38.2% retrace of wave 3 (leg 40 from 110→150): 140.56 and 134.72
            Assert.That(levels.SupportZone!.High, Is.EqualTo(140.56m).Within(0.01m));
            Assert.That(levels.SupportZone.Low, Is.EqualTo(134.72m).Within(0.01m));
        });
    }

    [Test]
    public void CompleteImpulse_UnfoldingIsCorrection_InvalidationAboveForBull()
    {
        var levels = ProjectionService.Project(Pivots(100m, 120m, 110m, 150m, 130m, 170m))!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Correction (ABC)"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(170m)); // wave 5 high
            // bullish impulse → correction is down → break ABOVE the high invalidates
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Above));
            Assert.That(levels.TargetZones, Is.Not.Empty);
        });
    }

    [Test]
    public void BearishWave4_InvalidationIsAbove()
    {
        // Down impulse: 100 → 80 → 90 → 50 (wave 4 pulling back up).
        var levels = ProjectionService.Project(Pivots(100m, 80m, 90m, 50m))!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.Bullish, Is.False);
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave 4"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(80m)); // end of wave 1 (a low)
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Above)); // resistance cap
        });
    }

    // ─── corrective projections ────────────────────────────────────────────────

    [Test]
    public void CorrectiveZigzag_UnfoldingB_InvalidationIsOriginOfA()
    {
        // Bearish zigzag (correcting a bull move): A down 200 → 140, B unfolding.
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m), StructureKind.Zigzag)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave B"));
            Assert.That(levels.Bullish, Is.False);
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(200m));
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Above));
            // 50–61.8% retrace of A (leg -60): 170 and 177.08
            Assert.That(levels.SupportZone!.Low, Is.EqualTo(170m).Within(0.01m));
            Assert.That(levels.SupportZone.High, Is.EqualTo(177.08m).Within(0.01m));
        });
    }

    [Test]
    public void CorrectiveFlat_UnfoldingB_HasDeepZoneAndNoHardInvalidation()
    {
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m), StructureKind.Flat)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.Invalidation, Is.Null, "a flat B may overshoot the origin");
            // 90–105% retrace of A: 194 and 203
            Assert.That(levels.SupportZone!.Low, Is.EqualTo(194m).Within(0.01m));
            Assert.That(levels.SupportZone.High, Is.EqualTo(203m).Within(0.01m));
        });
    }

    [Test]
    public void CorrectiveZigzag_UnfoldingC_TargetsAExtension()
    {
        // A: 200→140 (-60), B: back to 176; C projects 1.0–1.618× A down from 176.
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m, 176m), StructureKind.Zigzag)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave C"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(176m));
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Above));
            Assert.That(levels.TargetZones[0].High, Is.EqualTo(116m).Within(0.01m));  // 1.0×
            Assert.That(levels.TargetZones[0].Low, Is.EqualTo(78.92m).Within(0.01m)); // 1.618×
        });
    }

    [Test]
    public void CorrectiveComplete_ExpectsRecoveryAndInvalidatesBeyondC()
    {
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m, 176m, 120m), StructureKind.Zigzag)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Correction complete"));
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(120m));
            Assert.That(levels.TargetZones, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Triangle_UnfoldingE_MustStayInsideContractingRange()
    {
        // Bearish-opening triangle: A 200→140, B→176, C→150, D→170; E unfolding.
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m, 176m, 150m, 170m), StructureKind.Triangle)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Wave E (triangle)"));
            // E retraces the D-leg (150→170) downward; barrier is C's end (150).
            Assert.That(levels.Invalidation!.Price, Is.EqualTo(150m));
            Assert.That(levels.Invalidation.Side, Is.EqualTo(LevelSide.Below));
        });
    }

    [Test]
    public void Triangle_Complete_ProjectsThrustOfWidestLeg()
    {
        var levels = ProjectionService.ProjectCorrective(
            Pivots(200m, 140m, 176m, 150m, 170m, 155m), StructureKind.Triangle)!;

        Assert.Multiple(() =>
        {
            Assert.That(levels.UnfoldingWave, Is.EqualTo("Triangle thrust"));
            // Bearish A → thrust up; widest leg = 60 from E at 155: zone 200–215.
            Assert.That(levels.TargetZones[0].Low, Is.EqualTo(200m).Within(0.01m));
            Assert.That(levels.TargetZones[0].High, Is.EqualTo(215m).Within(0.01m));
        });
    }

    [Test]
    public void CorrectiveProjection_MotiveKind_ReturnsNull()
    {
        Assert.That(
            ProjectionService.ProjectCorrective(Pivots(100m, 130m), StructureKind.Impulse),
            Is.Null);
    }
}
