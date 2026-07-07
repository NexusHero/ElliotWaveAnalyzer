using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="ThesisFactGuard"/>: the anti-hallucination guard for the trade-thesis report (#187). A
/// narrative may cite only prices that appear somewhere in the fact sheet — current price,
/// invalidation, zones, confluence zones, risk levels, and scenario levels. A fabricated price is
/// rejected; wave numbers and percentages are allowed (AC1).
/// </summary>
[TestFixture]
public sealed class ThesisFactGuardTests
{
    private static readonly DateTimeOffset AsOf = new(2026, 1, 15, 0, 0, 0, TimeSpan.Zero);

    private static ThesisFactSheet Sheet() => new(
        "ACME", "1W: Impulse → 1D: Zigzag", Bullish: true,
        CurrentPrice: 150.00m,
        Invalidation: new PriceLevel(120.00m, LevelSide.Below, "inv", "end of 1"),
        EntryZone: new PriceZone(130.00m, 135.00m, "entry", "fib"),
        TargetZones: [new PriceZone(180.00m, 190.00m, "target", "ext")],
        Scale: FibScale.Linear,
        Risk: new RiskAssessment(true, null, true, 150.00m, 120.00m, 30.00m, 0.20m, 100m, 3.33m, 500m,
            [new TargetRisk(210.00m, 60.00m, 2.0m)]),
        ConfluenceZones: [new ConfluenceZone(178.00m, 182.00m, 3.5m, ZoneKind.Target, FibScale.Linear, [])],
        CalibratedProbability: 0.65m,
        Analogs: new AnalogStats(47, 32, 15, 0.68, 12.0, true),
        SentimentDivergences: [],
        Scenarios: [new Scenario(ScenarioRole.Alternate, "Alt 1", "Zigzag", false, 250.00m, true,
            null, null, 95.00m, 100.00m, "Medium", 0.6m, null, ProbabilityBasis.InsufficientData, false)],
        AsOf: AsOf);

    [Test]
    public void Passes_NarrativeCitingOnlyFactPrices_IsAllowed()
    {
        const string narrative =
            "Price at 150.00 holds above the 120.00 invalidation; a pullback into 130.00-135.00 targets 180.00.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.True);
    }

    [Test]
    public void Passes_NarrativeCitingRiskTargetPrice_IsAllowed()
    {
        const string narrative = "A stretch target near 210.00 offers roughly 2x the risk.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.True);
    }

    [Test]
    public void Passes_NarrativeCitingConfluenceZonePrice_IsAllowed()
    {
        const string narrative = "Confluence clusters between 178.00 and 182.00.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.True);
    }

    [Test]
    public void Passes_NarrativeCitingAlternateScenarioLevels_IsAllowed()
    {
        const string narrative = "The alternate zigzag would invalidate above 250.00, targeting 95.00-100.00.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithAFabricatedPrice_IsRejected()
    {
        const string narrative = "The count projects an ambitious run to 999.99 in short order.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.False);
    }

    [Test]
    public void Passes_WaveNumbersAndPercentages_AreAllowed()
    {
        const string narrative = "Wave 3 is extending; a 38.2% retrace with a 68% historical hit rate.";
        Assert.That(ThesisFactGuard.Passes(narrative, Sheet()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithNoNumbers_IsAllowed()
        => Assert.That(ThesisFactGuard.Passes("The structure remains constructive.", Sheet()), Is.True);

    [Test]
    public void Passes_PriceWithinTolerance_IsAllowed()
    {
        // 150.10 is within 0.5% of the 150.00 current price — rounding in prose is fine.
        Assert.That(ThesisFactGuard.Passes("Trading around 150.10 today.", Sheet()), Is.True);
    }

    [Test]
    public void FactPrices_IncludesEveryZoneRiskAndScenarioPrice()
    {
        var facts = ThesisFactGuard.FactPrices(Sheet());

        Assert.Multiple(() =>
        {
            Assert.That(facts, Does.Contain(150.00m)); // current price
            Assert.That(facts, Does.Contain(120.00m)); // invalidation
            Assert.That(facts, Does.Contain(130.00m).And.Contain(135.00m)); // entry zone
            Assert.That(facts, Does.Contain(180.00m).And.Contain(190.00m)); // target zone
            Assert.That(facts, Does.Contain(178.00m).And.Contain(182.00m)); // confluence zone
            Assert.That(facts, Does.Contain(210.00m)); // risk target
            Assert.That(facts, Does.Contain(250.00m)); // alternate scenario invalidation
            Assert.That(facts, Does.Contain(95.00m).And.Contain(100.00m)); // alternate scenario targets
        });
    }

    [Test]
    public void NullNarrativeOrSheet_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => ThesisFactGuard.Passes(null!, Sheet()));
            Assert.Throws<ArgumentNullException>(() => ThesisFactGuard.Passes("text", null!));
            Assert.Throws<ArgumentNullException>(() => ThesisFactGuard.FactPrices(null!));
        });
    }
}
