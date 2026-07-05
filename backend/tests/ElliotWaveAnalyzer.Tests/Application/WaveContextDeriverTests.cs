using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="WaveContextDeriver"/>: the constraint a coarse count hands down to
/// the finer timeframe — direction, class and price window of the wave currently unfolding.
/// Counts are built directly through <see cref="ProjectionService"/> so the unfolding wave is
/// deterministic and independent of the parser's ranking.
/// </summary>
[TestFixture]
public sealed class WaveContextDeriverTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveAnnotation A(int day, decimal price, string label) => new(T0.AddDays(day), price, label);

    private static WaveCandidate Candidate(string structure, WaveAnnotation origin, params WaveAnnotation[] waves)
    {
        var points = new List<WaveAnnotation> { origin };
        points.AddRange(waves);
        var levels = ProjectionService.Project(points);
        var report = new WaveRuleReport(true, [], []);
        return new WaveCandidate(0, structure, origin, waves, report, levels);
    }

    [Test]
    public void Derive_ImpulseInWave2_DemandsCorrectiveDownWithinTheOrigin()
    {
        // Origin + Wave 1 only → Wave 2 is unfolding: a pullback (corrective, down) that must hold
        // the origin (the invalidation), which becomes the low bound of the price window.
        var coarse = Candidate("Impulse", A(0, 100m, "1"), A(10, 120m, "1"));

        var context = WaveContextDeriver.Derive(coarse)!;

        Assert.Multiple(() =>
        {
            Assert.That(context.ParentWaveLabel, Does.Contain("Wave 2"));
            Assert.That(context.ExpectedDirection, Is.EqualTo(TrendDirection.Down));
            Assert.That(context.ExpectedClass, Is.EqualTo(StructureClass.Corrective));
            Assert.That(context.WindowLow, Is.EqualTo(100m));  // origin = invalidation
            Assert.That(context.WindowHigh, Is.EqualTo(120m)); // end of Wave 1
        });
    }

    [Test]
    public void Derive_ImpulseInWave3_DemandsMotiveUp()
    {
        // Origin + Wave 1 + Wave 2 → Wave 3 is unfolding: a motive thrust up toward the target.
        var coarse = Candidate("Impulse", A(0, 100m, "1"), A(10, 120m, "1"), A(20, 110m, "2"));

        var context = WaveContextDeriver.Derive(coarse)!;

        Assert.Multiple(() =>
        {
            Assert.That(context.ExpectedDirection, Is.EqualTo(TrendDirection.Up));
            Assert.That(context.ExpectedClass, Is.EqualTo(StructureClass.Motive));
        });
    }

    [Test]
    public void Derive_BearishWave2_DemandsCorrectiveUp()
    {
        // Mirror: a bearish impulse's Wave 2 pulls back up.
        var coarse = Candidate("Impulse", A(0, 120m, "1"), A(10, 100m, "1"));

        var context = WaveContextDeriver.Derive(coarse)!;

        Assert.Multiple(() =>
        {
            Assert.That(context.ExpectedDirection, Is.EqualTo(TrendDirection.Up));
            Assert.That(context.ExpectedClass, Is.EqualTo(StructureClass.Corrective));
        });
    }

    [Test]
    public void Derive_NoLevels_ReturnsNull()
    {
        var report = new WaveRuleReport(true, [], []);
        var noLevels = new WaveCandidate(0, "Impulse", A(0, 100m, "1"), [A(10, 120m, "1")], report, null);

        Assert.That(WaveContextDeriver.Derive(noLevels), Is.Null);
    }
}
