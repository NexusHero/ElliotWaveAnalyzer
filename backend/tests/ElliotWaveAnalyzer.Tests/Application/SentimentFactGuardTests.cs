using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The anti-hallucination guard for socionomics narratives: a mood score cited in prose must match a
/// figure the deterministic report actually computed (AC3). Grounded prose and wave-label integers pass.
/// </summary>
[TestFixture]
public sealed class SentimentFactGuardTests
{
    // Series readings 0.80 and 0.50; one bearish divergence at "5" citing the same two figures.
    private static SentimentReport Report()
    {
        var series = new[]
        {
            new SentimentPoint(new DateTime(2024, 1, 10), 0.80),
            new SentimentPoint(new DateTime(2024, 1, 20), 0.50),
        };
        var divergences = new[]
        {
            new MoodDivergence("5", new DateTime(2024, 1, 20), MoodDivergenceKind.Bearish, 0.80, 0.50),
        };
        return new SentimentReport(true, series, divergences);
    }

    [Test]
    public void Passes_GroundedNarrative_IsAccepted()
    {
        const string narrative = "Mood peaked at 0.80 into wave 3 but only reached 0.50 by wave 5.";
        Assert.That(SentimentFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_HallucinatedMoodScore_IsRejected()
    {
        Assert.That(SentimentFactGuard.Passes("Mood reached an extreme 0.95 at wave 5.", Report()), Is.False);
    }

    [Test]
    public void Passes_SmallWaveLabelIntegers_AreAllowed()
    {
        // Wave labels are bare integers without a decimal point — legitimate prose, not statistics.
        Assert.That(
            SentimentFactGuard.Passes("The divergence between wave 3 and wave 5 is the clearest signal.", Report()),
            Is.True);
    }

    [Test]
    public void Passes_NoNumbers_IsAccepted()
    {
        Assert.That(SentimentFactGuard.Passes("Mood is fading into the advance.", Report()), Is.True);
    }

    [Test]
    public void Passes_InjectedFabricatedReading_IsRejected()
    {
        // A narrative that invents a reading nowhere in the computed series/divergences.
        const string narrative = "An unusually strong mood reading of 0.99 confirms the breakout.";
        Assert.That(SentimentFactGuard.Passes(narrative, Report()), Is.False);
    }

    // ── #228 AC3: the guard matches digits via a language-agnostic regex, not English words —
    // German prose citing the report's own figures still passes, and a hallucinated figure in
    // German prose is still rejected exactly like the English case above.

    [Test]
    public void Passes_GermanGroundedNarrative_IsAccepted()
    {
        const string narrative = "Die Stimmung erreichte 0.80 bei Welle 3, fiel aber bis Welle 5 auf 0.50.";
        Assert.That(SentimentFactGuard.Passes(narrative, Report()), Is.True);
    }

    [Test]
    public void Passes_GermanHallucinatedMoodScore_IsRejected()
    {
        Assert.That(SentimentFactGuard.Passes("Die Stimmung erreichte ein Extrem von 0.95 bei Welle 5.", Report()), Is.False);
    }
}
