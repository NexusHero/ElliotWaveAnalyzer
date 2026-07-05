using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure scenario-tree logic: calibration→probability mapping (with the
/// insufficient-data floor), auto-switch promotion selection, and zone-entry idempotency.
/// </summary>
[TestFixture]
public sealed class ScenarioLogicTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static MarketCandle Candle(int day, decimal low, decimal high)
        => new(T0.AddDays(day), (low + high) / 2m, high, low, (low + high) / 2m, 1m);

    private static Scenario Alt(string label, decimal? score, decimal? probability = null) => new(
        ScenarioRole.Alternate, label, "Impulse", true, 100m, false,
        null, null, null, null, "medium", score, probability, ProbabilityBasis.Calibrated, Retired: false);

    // ── Probability mapping ────────────────────────────────────────────────────

    [Test]
    public void Probability_BucketWithEnoughSample_IsMeasuredHitRate()
    {
        // 20 concluded, 12 target-reached → hit-rate 0.6, publishable.
        var bucket = new CalibrationBucket("high", Total: 25, Concluded: 20, TargetReached: 12, Invalidated: 8, HitRate: 0.6m);

        var estimate = ScenarioProbability.From(bucket);

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.Calibrated));
            Assert.That(estimate.Probability, Is.EqualTo(0.6m));
        });
    }

    [Test]
    public void Probability_BucketBelowMinimumSample_IsInsufficientData()
    {
        // 9 concluded < default minimum 10 → withheld, no number.
        var bucket = new CalibrationBucket("low", Total: 12, Concluded: 9, TargetReached: 5, Invalidated: 4, HitRate: 0.556m);

        var estimate = ScenarioProbability.From(bucket);

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.InsufficientData));
            Assert.That(estimate.Probability, Is.Null);
        });
    }

    [Test]
    public void Probability_NullBucket_IsInsufficientData()
        => Assert.That(ScenarioProbability.From(null).Basis, Is.EqualTo(ProbabilityBasis.InsufficientData));

    // ── Auto-switch selection ──────────────────────────────────────────────────

    [Test]
    public void Promotion_TwoAlternates_PromotesHigherScored()
    {
        var promoted = ScenarioSwitch.SelectPromotion([Alt("Alt 1", 0.4m), Alt("Alt 2", 0.7m)]);

        Assert.That(promoted!.Label, Is.EqualTo("Alt 2"));
    }

    [Test]
    public void Promotion_NoAlternates_ReturnsNull()
        => Assert.That(ScenarioSwitch.SelectPromotion([]), Is.Null);

    [Test]
    public void Promotion_IgnoresRetiredAlternates()
    {
        var retired = Alt("Alt 1", 0.9m) with { Retired = true };
        var live = Alt("Alt 2", 0.3m);

        var promoted = ScenarioSwitch.SelectPromotion([retired, live]);

        Assert.That(promoted!.Label, Is.EqualTo("Alt 2"));
    }

    // ── Zone-entry idempotency ─────────────────────────────────────────────────

    [Test]
    public void ZoneEntry_PriceInsideZone_FirstRunAlerts_SecondRunDoesNot()
    {
        IReadOnlyList<MarketCandle> inside = [Candle(1, 95m, 105m)]; // overlaps [98,102]

        var first = ZoneEntryDecision.ShouldAlert(98m, 102m, alreadyAlerted: false, inside);
        // Second run models the persisted flag having been set after the first alert.
        var second = ZoneEntryDecision.ShouldAlert(98m, 102m, alreadyAlerted: true, inside);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
        });
    }

    [Test]
    public void ZoneEntry_PriceNeverInZone_DoesNotAlert()
    {
        IReadOnlyList<MarketCandle> below = [Candle(1, 80m, 90m)]; // never reaches [98,102]

        Assert.That(ZoneEntryDecision.ShouldAlert(98m, 102m, alreadyAlerted: false, below), Is.False);
    }

    [Test]
    public void ZoneEntry_NoZoneDefined_DoesNotAlert()
        => Assert.That(ZoneEntryDecision.ShouldAlert(null, null, false, [Candle(1, 95m, 105m)]), Is.False);
}
