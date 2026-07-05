using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="WaveContextConstraint"/>: finer counts contradicting the parent's
/// direction are hard-rejected; class/window disagreements are soft-penalized; the per-link
/// verdict reflects the best survivor. Candidates are hand-built so direction/class/range are exact.
/// </summary>
[TestFixture]
public sealed class WaveContextConstraintTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveCandidate Count(
        int id, string structure, decimal originPrice, decimal endPrice, decimal score, decimal? low = null, decimal? high = null)
    {
        var lo = low ?? Math.Min(originPrice, endPrice);
        var hi = high ?? Math.Max(originPrice, endPrice);
        var origin = new WaveAnnotation(T0, originPrice, "1");
        // Three labelled waves whose extremes touch lo/hi and end at endPrice.
        var waves = new[]
        {
            new WaveAnnotation(T0.AddDays(1), hi, "A"),
            new WaveAnnotation(T0.AddDays(2), lo, "B"),
            new WaveAnnotation(T0.AddDays(3), endPrice, "C"),
        };
        var report = new WaveRuleReport(true, [], []);
        return new WaveCandidate(id, structure, origin, waves, report, null) { Score = score };
    }

    // Parent: Wave-2-style pullback — corrective, down, price window 100..120.
    private static WaveContext DownCorrective() =>
        new("Wave 2", TrendDirection.Down, StructureClass.Corrective, 100m, 120m, WaveDegree.Primary);

    [Test]
    public void Apply_ContradictingDirection_RejectsThatCandidate()
    {
        var down = Count(1, "Zigzag", 118m, 104m, 0.7m, low: 102m, high: 118m); // nets down — valid
        var up = Count(2, "Impulse", 104m, 119m, 0.9m, low: 104m, high: 119m);  // nets up — contradicts

        var result = WaveContextConstraint.Apply(DownCorrective(), [down, up]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Ranked.Select(c => c.Id), Does.Not.Contain(2)); // up-count rejected
            Assert.That(result.Ranked.Select(c => c.Id), Does.Contain(1));
            Assert.That(result.Verdict, Is.EqualTo(ConsistencyVerdict.Consistent));
        });
    }

    [Test]
    public void Apply_AllCandidatesContradict_ReturnsContradictionWithReason()
    {
        var up1 = Count(1, "Impulse", 102m, 118m, 0.9m);
        var up2 = Count(2, "Zigzag", 104m, 116m, 0.8m);

        var result = WaveContextConstraint.Apply(DownCorrective(), [up1, up2]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Ranked, Is.Empty);
            Assert.That(result.Verdict, Is.EqualTo(ConsistencyVerdict.Contradiction));
            Assert.That(result.Reason, Is.Not.Empty);
        });
    }

    [Test]
    public void Apply_DirectionOkButClassMismatch_ProducesTensionAndPenalizes()
    {
        // Motive-down inside the window: direction agrees, but parent wanted a corrective.
        var motiveDown = Count(1, "Impulse", 118m, 104m, 0.8m, low: 102m, high: 118m);

        var result = WaveContextConstraint.Apply(DownCorrective(), [motiveDown]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Verdict, Is.EqualTo(ConsistencyVerdict.Tension));
            Assert.That(result.Reason, Does.Contain("corrective"));
            // Score penalized below the raw 0.8 by the class-mismatch multiplier.
            Assert.That(result.Ranked[0].Score, Is.LessThan(0.8m));
        });
    }

    [Test]
    public void Apply_ConsistentDirectionClassAndWindow_ReturnsConsistent()
    {
        var correctiveDown = Count(1, "Flat", 118m, 106m, 0.7m, low: 103m, high: 118m);

        var result = WaveContextConstraint.Apply(DownCorrective(), [correctiveDown]);

        Assert.That(result.Verdict, Is.EqualTo(ConsistencyVerdict.Consistent));
    }

    [Test]
    public void Apply_OutOfWindow_PenalizesAndFlagsTension()
    {
        // Corrective-down but the range blows far past the parent window (down to 40).
        var wild = Count(1, "Zigzag", 118m, 45m, 0.9m, low: 40m, high: 118m);

        var result = WaveContextConstraint.Apply(DownCorrective(), [wild]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Verdict, Is.EqualTo(ConsistencyVerdict.Tension));
            Assert.That(result.Ranked[0].Score, Is.LessThan(0.9m));
            Assert.That(result.Reason, Does.Contain("window"));
        });
    }

    [Test]
    public void Apply_RanksHigherScoreFirstAmongSurvivors()
    {
        var weak = Count(1, "Zigzag", 118m, 106m, 0.5m, low: 104m, high: 118m);
        var strong = Count(2, "Flat", 117m, 105m, 0.9m, low: 103m, high: 117m);

        var result = WaveContextConstraint.Apply(DownCorrective(), [weak, strong]);

        Assert.That(result.Ranked[0].Id, Is.EqualTo(2));
    }
}
