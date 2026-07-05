using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Feature extraction from a formed count: direction/structure carried through, reward:risk and
/// distance-to-invalidation from the same risk math the risk box uses, confluence strength on the
/// none/weak/strong basis, and a graceful zero when there is no invalidation to measure against.
/// </summary>
[TestFixture]
public sealed class SetupFeatureExtractorTests
{
    private static ContributingLevel Level(decimal price) => new(price, 1m, "fib");

    private static WaveLevels Levels(
        bool bullish = true,
        decimal? invalidation = 90m,
        decimal targetLow = 120m,
        int confluenceContributions = 0)
    {
        var zones = confluenceContributions == 0
            ? []
            : new[]
            {
                new ConfluenceZone(
                    118m, 122m, 3.0m, ZoneKind.Target, FibScale.Linear,
                    Enumerable.Range(0, confluenceContributions).Select(i => Level(118m + i)).ToList()),
            };

        return new WaveLevels(
            "Wave 4",
            bullish,
            invalidation is { } inv ? new PriceLevel(inv, LevelSide.Below, "inv", "End of Wave 1") : null,
            SupportZone: null,
            TargetZones: [new PriceZone(targetLow, targetLow + 10m, "t", "fib")],
            Alternative: null)
        {
            ConfluenceZones = zones,
        };
    }

    [Test]
    public void Extract_CarriesStructureDirectionAndScore()
    {
        var f = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(), score: 0.7m, currentPrice: 100m,
            rsiRegime: 0.55, macdRegime: 0.6, timeframe: "1d");

        Assert.Multiple(() =>
        {
            Assert.That(f.Structure, Is.EqualTo(StructureKind.Impulse));
            Assert.That(f.Bullish, Is.True);
            Assert.That(f.Score, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(f.Timeframe, Is.EqualTo("1d"));
            Assert.That(f.RsiRegime, Is.EqualTo(0.55).Within(1e-9));
            Assert.That(f.MacdRegime, Is.EqualTo(0.6).Within(1e-9));
        });
    }

    [Test]
    public void Extract_ComputesRewardToRiskAndDistanceFromGeometry()
    {
        // entry 100, invalidation 90 (10 below), target edge 120 (20 above) ⇒ R:R = 2, distance = 10%.
        var f = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(invalidation: 90m, targetLow: 120m), 0.7m, 100m, 0.5, 0.5, "1d");

        Assert.Multiple(() =>
        {
            Assert.That(f.RewardToRisk, Is.EqualTo(2.0).Within(1e-6));
            Assert.That(f.DistanceToInvalidationPct, Is.EqualTo(0.10).Within(1e-6));
        });
    }

    [Test]
    public void Extract_ConfluenceStrength_StrongWeakNone()
    {
        var strong = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(confluenceContributions: 2), 0.7m, 100m, 0.5, 0.5, "1d");
        var weak = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(confluenceContributions: 1), 0.7m, 100m, 0.5, 0.5, "1d");
        var none = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(confluenceContributions: 0), 0.7m, 100m, 0.5, 0.5, "1d");

        Assert.Multiple(() =>
        {
            Assert.That(strong.ConfluenceStrength, Is.EqualTo(1.0));
            Assert.That(weak.ConfluenceStrength, Is.EqualTo(0.5));
            Assert.That(none.ConfluenceStrength, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void Extract_NoInvalidation_YieldsZeroRewardAndDistance()
    {
        var f = SetupFeatureExtractor.Extract(
            StructureKind.Zigzag, Levels(invalidation: null), 0.5m, 100m, 0.5, 0.5, "1d");

        Assert.Multiple(() =>
        {
            Assert.That(f.RewardToRisk, Is.EqualTo(0.0));
            Assert.That(f.DistanceToInvalidationPct, Is.EqualTo(0.0));
        });
    }

    [Test]
    public void Extract_WrongSideInvalidation_YieldsZeroRewardAndDistance()
    {
        // Bullish but invalidation above entry ⇒ no valid stop ⇒ the risk features degrade to zero.
        var f = SetupFeatureExtractor.Extract(
            StructureKind.Impulse, Levels(bullish: true, invalidation: 110m), 0.7m, 100m, 0.5, 0.5, "1d");

        Assert.Multiple(() =>
        {
            Assert.That(f.RewardToRisk, Is.EqualTo(0.0));
            Assert.That(f.DistanceToInvalidationPct, Is.EqualTo(0.0));
        });
    }
}
