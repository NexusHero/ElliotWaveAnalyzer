using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Domain;

/// <summary>
/// The client-shape mapping: analogs flattened to their display fields, stats and narrative carried
/// through, and an explicit insufficient response when there is nothing to compare.
/// </summary>
[TestFixture]
public sealed class AnalogResponseTests
{
    private static readonly SetupFeatures Features =
        new(StructureKind.Impulse, true, "1d", 0.7, 0.5, 2.0, 0.08, 0.55, 0.6);

    [Test]
    public void From_MapsAnalogsAndCarriesStatsAndNarrative()
    {
        var formed = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var analog = new HistoricalAnalog(
            new HistoricalSetup("BTC", formed, formed.AddDays(12), AnalysisOutcome.TargetReached, Features),
            0.87);
        var report = new AnalogReport(
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            [analog],
            new AnalogStats(10, 7, 3, 0.7, 11.0, Sufficient: true))
        {
            Narrative = "Constructive.",
        };

        var dto = AnalogResponse.From("BTC", "1D", report);

        Assert.Multiple(() =>
        {
            Assert.That(dto.Symbol, Is.EqualTo("BTC"));
            Assert.That(dto.Timeframe, Is.EqualTo("1D"));
            Assert.That(dto.Narrative, Is.EqualTo("Constructive."));
            Assert.That(dto.Stats.HitRate, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(dto.Analogs, Has.Count.EqualTo(1));
            Assert.That(dto.Analogs[0].Outcome, Is.EqualTo("TargetReached"));
            Assert.That(dto.Analogs[0].Structure, Is.EqualTo("Impulse"));
            Assert.That(dto.Analogs[0].Similarity, Is.EqualTo(0.87).Within(1e-9));
            Assert.That(dto.Analogs[0].ResolutionDays, Is.EqualTo(12.0).Within(1e-9));
        });
    }

    [Test]
    public void Insufficient_HasEmptyAnalogsAndAReason()
    {
        var dto = AnalogResponse.Insufficient("ETH", "1W", "No current count.");

        Assert.Multiple(() =>
        {
            Assert.That(dto.Analogs, Is.Empty);
            Assert.That(dto.Stats.Sufficient, Is.False);
            Assert.That(dto.NarrativeUnavailableReason, Is.EqualTo("No current count."));
        });
    }
}
