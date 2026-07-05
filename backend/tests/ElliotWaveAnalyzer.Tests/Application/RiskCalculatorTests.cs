using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The risk math (REQ-030): stop distance, reward:risk and position sizing derived from a count's
/// geometry — deterministic and direction-aware, with an entry on the wrong side of the invalidation
/// producing an explicit "no valid stop" rather than a negative or infinite size.
/// </summary>
[TestFixture]
public sealed class RiskCalculatorTests
{
    [Test]
    public void Assess_BullishEntryAboveStop_ComputesStopDistanceRewardAndSize()
    {
        // 1% of a 10k account = 100 risked; stop distance 10 → size 10 units.
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 90m, targets: [130m], bullish: true, riskCapital: 100m);

        Assert.Multiple(() =>
        {
            Assert.That(risk.HasValidStop, Is.True);
            Assert.That(risk.StopDistanceAbs, Is.EqualTo(10m));
            Assert.That(risk.StopDistancePct, Is.EqualTo(0.10m));
            Assert.That(risk.Targets, Has.Count.EqualTo(1));
            Assert.That(risk.Targets[0].RewardToRisk, Is.EqualTo(3.0m));
            Assert.That(risk.SuggestedSize, Is.EqualTo(10m));
            Assert.That(risk.Notional, Is.EqualTo(1000m));
        });
    }

    [Test]
    public void Assess_BearishEntryBelowStop_MirrorsTheBullishCase()
    {
        // Short: entry 100, stop 110 above, target 70. Distance 10 (10%), reward 30 → R:R 3.0, size 10.
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 110m, targets: [70m], bullish: false, riskCapital: 100m);

        Assert.Multiple(() =>
        {
            Assert.That(risk.HasValidStop, Is.True);
            Assert.That(risk.StopDistanceAbs, Is.EqualTo(10m));
            Assert.That(risk.StopDistancePct, Is.EqualTo(0.10m));
            Assert.That(risk.Targets[0].RewardAbs, Is.EqualTo(30m));
            Assert.That(risk.Targets[0].RewardToRisk, Is.EqualTo(3.0m));
            Assert.That(risk.SuggestedSize, Is.EqualTo(10m));
            Assert.That(risk.Notional, Is.EqualTo(1000m));
        });
    }

    [Test]
    public void Assess_LongEntryOnWrongSideOfInvalidation_NoValidStopAndNoSize()
    {
        // Stop at or above entry for a long → no room; must not produce a negative/infinite size.
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 105m, targets: [130m], bullish: true, riskCapital: 100m);

        Assert.Multiple(() =>
        {
            Assert.That(risk.HasValidStop, Is.False);
            Assert.That(risk.NoStopReason, Is.Not.Null.And.Not.Empty);
            Assert.That(risk.SuggestedSize, Is.Null);
            Assert.That(risk.Notional, Is.Null);
            Assert.That(risk.Targets, Is.Empty);
        });
    }

    [Test]
    public void Assess_StopExactlyAtEntry_NoValidStop()
    {
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 100m, targets: [130m], bullish: true, riskCapital: 100m);

        Assert.That(risk.HasValidStop, Is.False);
    }

    [Test]
    public void Assess_MultipleTargets_ReportsRewardToRiskPerTargetAscending()
    {
        var risk = RiskCalculator.Assess(
            entry: 100m, invalidation: 90m, targets: [160m, 130m, 145m], bullish: true, riskCapital: 100m);

        Assert.Multiple(() =>
        {
            Assert.That(risk.Targets.Select(t => t.Price), Is.EqualTo(new[] { 130m, 145m, 160m }));
            Assert.That(risk.Targets.Select(t => t.RewardToRisk), Is.EqualTo(new[] { 3.0m, 4.5m, 6.0m }));
        });
    }

    [TestCase(0)]
    [TestCase(-50)]
    public void Assess_NonPositiveRiskCapital_KeepsStopButOmitsSize(decimal riskCapital)
    {
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 90m, targets: [130m], bullish: true, riskCapital: riskCapital);

        Assert.Multiple(() =>
        {
            Assert.That(risk.HasValidStop, Is.True);
            Assert.That(risk.StopDistanceAbs, Is.EqualTo(10m));
            Assert.That(risk.Targets[0].RewardToRisk, Is.EqualTo(3.0m), "R:R is geometric and stands without capital");
            Assert.That(risk.SuggestedSize, Is.Null, "no divide-by-zero / nonsense size");
            Assert.That(risk.Notional, Is.Null);
        });
    }

    [Test]
    public void Assess_NoTargets_StillProducesStopAndSize()
    {
        var risk = RiskCalculator.Assess(entry: 100m, invalidation: 90m, targets: [], bullish: true, riskCapital: 100m);

        Assert.Multiple(() =>
        {
            Assert.That(risk.HasValidStop, Is.True);
            Assert.That(risk.SuggestedSize, Is.EqualTo(10m));
            Assert.That(risk.Targets, Is.Empty);
        });
    }
}
