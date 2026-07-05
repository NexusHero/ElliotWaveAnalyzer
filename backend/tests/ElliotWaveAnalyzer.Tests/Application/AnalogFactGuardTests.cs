using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The anti-hallucination guard: a narrative may only cite the rates and counts the deterministic
/// report computed. A fabricated hit-rate, sample size or day-count is rejected (AC4); grounded prose
/// and small prose integers pass.
/// </summary>
[TestFixture]
public sealed class AnalogFactGuardTests
{
    // Hit-rate 0.68 (17 of 25), 8 invalidated, median 12 days; analogs resolved in 2023.
    private static AnalogReport Report()
    {
        var features = new SetupFeatures(StructureKind.Impulse, true, "1d", 0.7, 0.5, 2.0, 0.08, 0.55, 0.6);
        var formed = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var analogs = new[] { 10.0, 12.0, 14.0 }
            .Select(d => new HistoricalAnalog(
                new HistoricalSetup("SYM", formed, formed.AddDays(d), AnalysisOutcome.TargetReached, features),
                0.9))
            .ToList();
        var stats = new AnalogStats(25, 17, 8, 0.68, 12.0, Sufficient: true);
        return new AnalogReport(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), analogs, stats);
    }

    [Test]
    public void Passes_GroundedNarrative_IsAccepted()
    {
        const string narrative = "About 68% of the 25 analogs reached target, with a median of 12 days.";
        Assert.That(AnalogFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_ComplementRate_IsAccepted()
    {
        // 32% = 100 − 68; the "miss rate" is a legitimate framing of the same fact.
        Assert.That(AnalogFactGuard.Passes("Roughly 32% invalidated instead.", Report()), Is.True);
    }

    [Test]
    public void Passes_HallucinatedHitRate_IsRejected()
    {
        Assert.That(AnalogFactGuard.Passes("A strong 82% reached their target.", Report()), Is.False);
    }

    [Test]
    public void Passes_HallucinatedSampleCount_IsRejected()
    {
        Assert.That(AnalogFactGuard.Passes("Across 47 prior setups, most worked out.", Report()), Is.False);
    }

    [Test]
    public void Passes_HallucinatedMedianDays_IsRejected()
    {
        Assert.That(AnalogFactGuard.Passes("They resolved in a median of 40 days.", Report()), Is.False);
    }

    [Test]
    public void Passes_SmallProseIntegers_AreAllowed()
    {
        // Wave labels and "the two that failed" are prose, not fabricated statistics.
        Assert.That(
            AnalogFactGuard.Passes("The two clearest failures both had a shallow wave 2.", Report()),
            Is.True);
    }

    [Test]
    public void Passes_MentionedAnalogYear_IsAllowed()
    {
        Assert.That(AnalogFactGuard.Passes("The closest analog resolved back in 2023.", Report()), Is.True);
    }

    [Test]
    public void Passes_FabricatedYear_IsRejected()
    {
        Assert.That(AnalogFactGuard.Passes("A similar setup appeared in 2019.", Report()), Is.False);
    }

    [Test]
    public void Passes_NoNumbers_IsAccepted()
    {
        Assert.That(AnalogFactGuard.Passes("The analogs skew bullish and constructive.", Report()), Is.True);
    }
}
