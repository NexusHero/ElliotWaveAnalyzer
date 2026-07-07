using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Elliott's socionomic core, made measurable: price extending further than a count's conviction wave
/// while mood does not confirm is flagged as a divergence (AC2). Pure — never touches the pivots it
/// reads (AC5: the count's own geometry is never affected by this pass).
/// </summary>
[TestFixture]
public sealed class MoodDivergenceDetectorTests
{
    private static WaveAnnotation P(int day, decimal price, string label) =>
        new(new DateTime(2024, 1, 1).AddDays(day), price, label);

    private static SentimentPoint S(int day, double score) =>
        new(new DateTime(2024, 1, 1).AddDays(day), score);

    [Test]
    public void Detect_MoodPeaksAtWave3ButPriceExtendsToWave5_FlagsBearishDivergenceAtWave5()
    {
        var pivots = new[]
        {
            P(0, 100m, "1"),
            P(5, 90m, "2"),
            P(10, 140m, "3"), // conviction wave: price 140, mood 0.8 (peak)
            P(15, 120m, "4"),
            P(20, 160m, "5"), // extension wave: price 160 (new high), mood 0.5 (did not confirm)
        };
        var sentiment = new[] { S(10, 0.8), S(20, 0.5) };

        var divergences = MoodDivergenceDetector.Detect(pivots, sentiment);

        Assert.That(divergences, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(divergences[0].PivotLabel, Is.EqualTo("5"));
            Assert.That(divergences[0].Kind, Is.EqualTo(MoodDivergenceKind.Bearish));
            Assert.That(divergences[0].EarlierMood, Is.EqualTo(0.8));
            Assert.That(divergences[0].LaterMood, Is.EqualTo(0.5));
        });
    }

    [Test]
    public void Detect_MoodConfirmsTheNewHigh_NoDivergence()
    {
        var pivots = new[] { P(10, 140m, "3"), P(20, 160m, "5") };
        var sentiment = new[] { S(10, 0.5), S(20, 0.8) }; // mood also rose — confirmed

        Assert.That(MoodDivergenceDetector.Detect(pivots, sentiment), Is.Empty);
    }

    [Test]
    public void Detect_MoodTroughAtWaveAButPriceExtendsLowerToWaveC_FlagsBullishDivergenceAtWaveC()
    {
        var pivots = new[]
        {
            P(0, 100m, "A"), // price 100, mood -0.7 (trough)
            P(10, 110m, "B"),
            P(20, 80m, "C"), // price 80 (new low), mood -0.3 (did not confirm — less negative)
        };
        var sentiment = new[] { S(0, -0.7), S(20, -0.3) };

        var divergences = MoodDivergenceDetector.Detect(pivots, sentiment);

        Assert.That(divergences, Has.Count.EqualTo(1));
        Assert.That(divergences[0].PivotLabel, Is.EqualTo("C"));
        Assert.That(divergences[0].Kind, Is.EqualTo(MoodDivergenceKind.Bullish));
    }

    [Test]
    public void Detect_MissingConvictionOrExtensionLabel_NoDivergence()
    {
        var pivots = new[] { P(0, 100m, "1"), P(10, 120m, "2") }; // no wave 3 or 5 yet
        var sentiment = new[] { S(0, 0.1), S(10, 0.9) };

        Assert.That(MoodDivergenceDetector.Detect(pivots, sentiment), Is.Empty);
    }

    [Test]
    public void Detect_NoSentimentCoverage_ReturnsEmptyRatherThanGuessing()
    {
        var pivots = new[] { P(10, 140m, "3"), P(20, 160m, "5") };

        Assert.That(MoodDivergenceDetector.Detect(pivots, []), Is.Empty);
    }

    [Test]
    public void Detect_NearestReading_UsesClosestDateWhenExactDateMissing()
    {
        var pivots = new[] { P(10, 140m, "3"), P(20, 160m, "5") };
        // Readings a day off each pivot — nearest-date lookup should still find them.
        var sentiment = new[] { S(9, 0.8), S(21, 0.5) };

        var divergences = MoodDivergenceDetector.Detect(pivots, sentiment);

        Assert.That(divergences, Has.Count.EqualTo(1));
    }

    [Test]
    public void Detect_NeverMutatesTheInputPivots()
    {
        var pivots = new[] { P(10, 140m, "3"), P(20, 160m, "5") };
        var snapshot = pivots.ToArray();
        var sentiment = new[] { S(10, 0.8), S(20, 0.5) };

        MoodDivergenceDetector.Detect(pivots, sentiment);

        Assert.That(pivots, Is.EqualTo(snapshot));
    }
}
