using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The anti-hallucination guard: a narrative may cite only the position's fact prices. A fabricated
/// price is rejected; the real prices, wave numbers and small percentages are allowed.
/// </summary>
[TestFixture]
public sealed class PositionFactGuardTests
{
    private static PositionBrief Brief() => new(
        "US0000000001", "ACME", "Acme Corp", "1W: Impulse → 1D: Zigzag", Bullish: true,
        CurrentPrice: 150.00m,
        Invalidation: new PriceLevel(120.00m, LevelSide.Below, "inv", "end of 1"),
        EntryZone: new PriceZone(130.00m, 135.00m, "entry", "fib"),
        TargetZones: [new PriceZone(180.00m, 190.00m, "target", "ext")],
        Scale: FibScale.Linear);

    [Test]
    public void Passes_NarrativeCitingOnlyFactPrices_IsAllowed()
    {
        const string narrative =
            "Price at 150.00 holds above the 120.00 invalidation; a pullback into 130.00–135.00 targets 180.00.";
        Assert.That(PositionFactGuard.Passes(narrative, Brief()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithAFabricatedPrice_IsRejected()
    {
        // 999.99 appears nowhere in the fact sheet — the guard must fire.
        const string narrative = "The count projects an ambitious run to 999.99 in short order.";
        Assert.That(PositionFactGuard.Passes(narrative, Brief()), Is.False);
    }

    [Test]
    public void Passes_WaveNumbersAndSmallPercentages_AreAllowed()
    {
        const string narrative = "Wave 3 is extending; a 38.2% retrace of wave 2 would be normal.";
        Assert.That(PositionFactGuard.Passes(narrative, Brief()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithNoNumbers_IsAllowed()
        => Assert.That(PositionFactGuard.Passes("The structure remains constructive.", Brief()), Is.True);

    [Test]
    public void Passes_PriceWithinTolerance_IsAllowed()
    {
        // 150.10 is within 0.5% of the 150.00 current price — rounding in prose is fine.
        Assert.That(PositionFactGuard.Passes("Trading around 150.10 today.", Brief()), Is.True);
    }

    // ── #228 AC3: the guard matches digits via a language-agnostic regex, not English words —
    // German prose citing the brief's own prices still passes, and a hallucinated price in German
    // prose is still rejected exactly like the English case above.

    [Test]
    public void Passes_GermanNarrativeCitingOnlyFactPrices_IsAllowed()
    {
        const string narrative =
            "Der Kurs hält bei 150.00 über der Invalidierung von 120.00; ein Rücksetzer auf 130.00 favorisiert 180.00.";
        Assert.That(PositionFactGuard.Passes(narrative, Brief()), Is.True);
    }

    [Test]
    public void Passes_GermanNarrativeWithAFabricatedPrice_IsRejected()
    {
        const string narrative = "Das Ziel liegt bei 999.99.";
        Assert.That(PositionFactGuard.Passes(narrative, Brief()), Is.False);
    }
}
