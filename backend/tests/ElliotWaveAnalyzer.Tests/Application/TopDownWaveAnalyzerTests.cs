using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="TopDownWaveAnalyzer"/> on a synthetic fractal fixture: a coarse
/// series whose best count is corrective-down, a finer series that agrees (consistent) and one
/// that genuinely disagrees (contradiction), plus determinism of the serialized chain.
/// </summary>
[TestFixture]
public sealed class TopDownWaveAnalyzerTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SwingPivot P(int day, decimal price, bool high) => new(T0.AddDays(day), price, high);

    // A five-leg advance whose best deterministic count is read as a completing correction: the
    // active wave is corrective and travels DOWN, within a window topped by the swing high (170).
    private static IReadOnlyList<SwingPivot> CoarseCorrectiveDown() =>
    [
        P(0, 100, false), P(10, 120, true), P(20, 110, false),
        P(30, 150, true), P(40, 130, false), P(50, 170, true),
    ];

    // A zigzag that nets DOWN (168 → 138), sitting inside the coarse window — a consistent child.
    private static IReadOnlyList<SwingPivot> FineDownZigzag() =>
    [
        P(0, 168, true), P(3, 150, false), P(6, 158, true), P(9, 138, false),
    ];

    // A move that nets UP (138 → 168) — it cannot be the substructure of a down wave.
    private static IReadOnlyList<SwingPivot> FineUp() =>
    [
        P(0, 138, false), P(3, 150, true), P(6, 142, false), P(9, 168, true),
    ];

    [Test]
    public void Analyze_CoarseThenConsistentFine_LinksConsistent()
    {
        var result = TopDownWaveAnalyzer.Analyze(
        [
            new TimeframePivots("1W", CoarseCorrectiveDown()),
            new TimeframePivots("1D", FineDownZigzag()),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Timeframes, Has.Count.EqualTo(2));
            Assert.That(result.Links, Has.Count.EqualTo(1));
            Assert.That(result.Timeframes[0].Degree, Is.EqualTo(WaveDegree.Primary));
            Assert.That(result.Timeframes[1].Degree, Is.EqualTo(WaveDegree.Intermediate));
            // The coarse count imposes a corrective-down context on the finer chart.
            Assert.That(result.Timeframes[0].ImposedContext!.ExpectedDirection, Is.EqualTo(TrendDirection.Down));
            Assert.That(result.Timeframes[0].ImposedContext!.ExpectedClass, Is.EqualTo(StructureClass.Corrective));
            Assert.That(result.Links[0].ParentInterval, Is.EqualTo("1W"));
            Assert.That(result.Links[0].ChildInterval, Is.EqualTo("1D"));
            Assert.That(result.Links[0].Verdict, Is.EqualTo(ConsistencyVerdict.Consistent));
        });
    }

    [Test]
    public void Analyze_ContradictingFine_ReportsContradiction()
    {
        var result = TopDownWaveAnalyzer.Analyze(
        [
            new TimeframePivots("1W", CoarseCorrectiveDown()),
            new TimeframePivots("1D", FineUp()),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Links[0].Verdict, Is.EqualTo(ConsistencyVerdict.Contradiction));
            Assert.That(result.Links[0].Reason, Is.Not.Empty);
        });
    }

    [Test]
    public void Analyze_SingleTimeframe_ProducesNoLinks()
    {
        var result = TopDownWaveAnalyzer.Analyze([new TimeframePivots("1W", CoarseCorrectiveDown())]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Timeframes, Has.Count.EqualTo(1));
            Assert.That(result.Links, Is.Empty);
        });
    }

    [Test]
    public void Analyze_SameInput_SerializesIdentically()
    {
        var input = new[]
        {
            new TimeframePivots("1W", CoarseCorrectiveDown()),
            new TimeframePivots("1D", FineDownZigzag()),
        };

        var first = JsonSerializer.Serialize(TopDownWaveAnalyzer.Analyze(input));
        var second = JsonSerializer.Serialize(TopDownWaveAnalyzer.Analyze(input));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Analyze_Summary_ChainsIntervalsWithArrows()
    {
        var result = TopDownWaveAnalyzer.Analyze(
        [
            new TimeframePivots("1W", CoarseCorrectiveDown()),
            new TimeframePivots("1D", FineDownZigzag()),
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary, Does.Contain("1W:"));
            Assert.That(result.Summary, Does.Contain("→"));
            Assert.That(result.Summary, Does.Contain("1D:"));
        });
    }

    [Test]
    public void Analyze_EmptyPivots_YieldsNoCountButStaysWellFormed()
    {
        var result = TopDownWaveAnalyzer.Analyze([new TimeframePivots("1W", [])]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Timeframes, Has.Count.EqualTo(1));
            Assert.That(result.Timeframes[0].BestCount, Is.Null);
            Assert.That(result.Timeframes[0].ImposedContext, Is.Null);
        });
    }
}
