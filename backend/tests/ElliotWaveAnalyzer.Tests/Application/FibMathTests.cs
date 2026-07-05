using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="FibMath"/>: linear vs. log retracement/extension against
/// hand-computed values, and the auto-scale threshold.
/// </summary>
[TestFixture]
public sealed class FibMathTests
{
    [Test]
    public void Retrace_Linear_MatchesHandComputed()
    {
        // 100 − 0.618 × (100 − 10) = 44.38
        var level = FibMath.Retrace(10m, 100m, 0.618m, FibScale.Linear);

        Assert.That(level, Is.EqualTo(44.38m).Within(0.0001m));
    }

    [Test]
    public void Retrace_Log_MatchesHandComputed()
    {
        // exp(ln100 − 0.618 × (ln100 − ln10)) = exp(3.18217…) ≈ 24.099
        var level = FibMath.Retrace(10m, 100m, 0.618m, FibScale.Log);

        Assert.That((double)level, Is.EqualTo(24.099).Within(0.01));
    }

    [Test]
    public void Retrace_LogAndLinear_Differ_ForLargeRanges()
    {
        var linear = FibMath.Retrace(10m, 100m, 0.618m, FibScale.Linear);
        var log = FibMath.Retrace(10m, 100m, 0.618m, FibScale.Log);

        Assert.That(log, Is.Not.EqualTo(linear));
    }

    [Test]
    public void Extend_Log_DoublesAgain_ForADoublingLeg()
    {
        // Leg 50→100 is ×2; a 1.0× log extension from 100 doubles again → 200.
        var level = FibMath.Extend(100m, 50m, 100m, 1.0m, FibScale.Log);

        Assert.That((double)level, Is.EqualTo(200.0).Within(0.0001));
    }

    [Test]
    public void Extend_Linear_MatchesHandComputed()
    {
        // 100 + 1.618 × (100 − 50) = 180.9
        var level = FibMath.Extend(100m, 50m, 100m, 1.618m, FibScale.Linear);

        Assert.That(level, Is.EqualTo(180.9m).Within(0.0001m));
    }

    [TestCase(29, ExpectedResult = FibScale.Linear)] // 10→29 ratio 2.9
    [TestCase(31, ExpectedResult = FibScale.Log)]    // 10→31 ratio 3.1
    public FibScale AutoSelect_SwitchesAtThreeTimesRange(int high)
        => FibMath.AutoSelect([10m, high]);

    [Test]
    public void AutoSelect_TooFewPoints_IsLinear()
        => Assert.That(FibMath.AutoSelect([42m]), Is.EqualTo(FibScale.Linear));
}
