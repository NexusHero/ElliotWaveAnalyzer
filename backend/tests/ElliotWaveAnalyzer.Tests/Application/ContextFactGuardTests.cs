using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="ContextFactGuard"/>: a narrative may cite only correlation/percent-move numbers the
/// context report actually computed. A fabricated correlation is rejected; dates, wave labels and
/// percentages are legitimate, unchecked prose (#188, AC1).
/// </summary>
[TestFixture]
public sealed class ContextFactGuardTests
{
    private static ContextReport Report() => new(
        HasCatalystCoverage: true,
        CatalystFlags: [new CatalystFlag(
            new CatalystEvent(new DateTime(2026, 3, 15), "FOMC Rate Decision", "TestCalendar"),
            new DateTime(2026, 3, 17), DaysFromTurn: 2)],
        HasIntermarketCoverage: true,
        IntermarketSignals: [new IntermarketSignal("DXY", -0.62, -0.014m, IntermarketSignalKind.Support)]);

    [Test]
    public void Passes_NarrativeCitingTheComputedCorrelationAndMove_IsAllowed()
    {
        const string narrative = "DXY (correlation -0.62) moved -0.014, corroborating the bullish thesis.";
        Assert.That(ContextFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_NarrativeCitingTheCatalystDate_IsAllowed()
    {
        // 2026-03-15 is a legitimate date, not a fabricated correlation — decimal-only checking must
        // not misfire on it.
        const string narrative = "The FOMC Rate Decision on 2026-03-15 falls 2 days from the projected turn.";
        Assert.That(ContextFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithAFabricatedCorrelation_IsRejected()
    {
        const string narrative = "A striking correlation of 0.91 confirms the read.";
        Assert.That(ContextFactGuard.Passes(narrative, Report()), Is.False);
    }

    [Test]
    public void Passes_NarrativeWithAFabricatedPercentMove_IsRejected()
    {
        const string narrative = "DXY has fallen 3.75 over the same window.";
        Assert.That(ContextFactGuard.Passes(narrative, Report()), Is.False);
    }

    [Test]
    public void Passes_PercentagesAndWaveLabels_AreAllowed()
    {
        const string narrative = "Wave 3 is extending; DXY moved -1.4% while a 38.2% retrace held.";
        Assert.That(ContextFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_NumberWithinTolerance_IsAllowed()
    {
        // -0.615 is within 0.5% of the -0.62 fact — rounding in prose is fine.
        Assert.That(ContextFactGuard.Passes("Correlation runs about -0.615.", Report()), Is.True);
    }

    [Test]
    public void Passes_NarrativeWithNoNumbers_IsAllowed()
        => Assert.That(ContextFactGuard.Passes("Context corroborates the bullish read.", Report()), Is.True);

    [Test]
    public void FactNumbers_IncludesEverySignalAndFlagNumber()
    {
        var facts = ContextFactGuard.FactNumbers(Report());

        Assert.Multiple(() =>
        {
            Assert.That(facts, Does.Contain(-0.62m));
            Assert.That(facts, Does.Contain(-0.014m));
            Assert.That(facts, Does.Contain(2m));
        });
    }

    [Test]
    public void NullNarrativeOrReport_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => ContextFactGuard.Passes(null!, Report()));
            Assert.Throws<ArgumentNullException>(() => ContextFactGuard.Passes("text", null!));
            Assert.Throws<ArgumentNullException>(() => ContextFactGuard.FactNumbers(null!));
        });
    }
}
