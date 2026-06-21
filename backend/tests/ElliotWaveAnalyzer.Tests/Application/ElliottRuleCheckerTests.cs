using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the deterministic <see cref="ElliottRuleChecker"/>. Pivots are read as
/// consecutive points P0..P5 (origin + waves 1..5).
/// </summary>
[TestFixture]
public sealed class ElliottRuleCheckerTests
{
    private static IReadOnlyList<WaveAnnotation> Pivots(params decimal[] prices)
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return [.. prices.Select((p, i) => new WaveAnnotation(start.AddDays(i), p, "3"))];
    }

    private static RuleStatus Rule1(WaveRuleReport r) => r.Rules[0].Status;
    private static RuleStatus Rule2(WaveRuleReport r) => r.Rules[1].Status;
    private static RuleStatus Rule3(WaveRuleReport r) => r.Rules[2].Status;

    [Test]
    public void ValidBullishImpulse_AllRulesPass()
    {
        var report = ElliottRuleChecker.Check(Pivots(100, 120, 110, 150, 130, 170));

        Assert.That(report.BullishAssumed, Is.True);
        Assert.That(Rule1(report), Is.EqualTo(RuleStatus.Pass));
        Assert.That(Rule2(report), Is.EqualTo(RuleStatus.Pass));
        Assert.That(Rule3(report), Is.EqualTo(RuleStatus.Pass));
    }

    [Test]
    public void Wave2BeyondOrigin_Rule1Fails()
    {
        // P2 = 95 dips below the origin (100).
        var report = ElliottRuleChecker.Check(Pivots(100, 120, 95, 150, 130, 170));

        Assert.That(Rule1(report), Is.EqualTo(RuleStatus.Fail));
    }

    [Test]
    public void Wave3Shortest_Rule2Fails()
    {
        // wave1=30, wave3=25, wave5=68 → wave 3 is the shortest.
        var report = ElliottRuleChecker.Check(Pivots(100, 130, 115, 140, 132, 200));

        Assert.That(Rule2(report), Is.EqualTo(RuleStatus.Fail));
    }

    [Test]
    public void Wave4OverlapsWave1_Rule3Fails()
    {
        // P4 = 115 falls back into Wave 1's territory (top = 120).
        var report = ElliottRuleChecker.Check(Pivots(100, 120, 110, 150, 115, 170));

        Assert.That(Rule3(report), Is.EqualTo(RuleStatus.Fail));
    }

    [Test]
    public void TooFewPivots_RulesAreIndeterminate()
    {
        var report = ElliottRuleChecker.Check(Pivots(100, 120));

        Assert.That(Rule1(report), Is.EqualTo(RuleStatus.Indeterminate));
        Assert.That(Rule2(report), Is.EqualTo(RuleStatus.Indeterminate));
        Assert.That(Rule3(report), Is.EqualTo(RuleStatus.Indeterminate));
    }

    [Test]
    public void ComputesFibonacciRatios()
    {
        var report = ElliottRuleChecker.Check(Pivots(100, 120, 110, 150, 130, 170));

        var retrace = report.Ratios.First(r => r.Name.Contains("Wave 2"));
        Assert.That(retrace.Ratio, Is.EqualTo(0.5m));
        var extension = report.Ratios.First(r => r.Name.Contains("Wave 3"));
        Assert.That(extension.Ratio, Is.EqualTo(2.0m));
    }
}
