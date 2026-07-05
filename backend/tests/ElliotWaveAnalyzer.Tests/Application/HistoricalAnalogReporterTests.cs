using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The end-to-end deterministic read: retrieve + aggregate composed into one report. Same query and
/// corpus ⇒ byte-identical report (AC5); too few analogs ⇒ an explicit insufficient state (AC6).
/// </summary>
[TestFixture]
public sealed class HistoricalAnalogReporterTests
{
    private static readonly DateTimeOffset AsOf = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private static SetupFeatures Features(StructureKind s = StructureKind.Impulse, double score = 0.7) =>
        new(s, true, "1d", score, 0.5, 2.0, 0.08, 0.55, 0.6);

    private static HistoricalSetup Concluded(string symbol, int daysAgo, AnalysisOutcome outcome, SetupFeatures f)
    {
        var concluded = AsOf.AddDays(-daysAgo);
        return new HistoricalSetup(symbol, concluded.AddDays(-10), concluded, outcome, f);
    }

    private static IReadOnlyList<HistoricalSetup> Corpus() =>
        Enumerable.Range(0, 8)
            .Select(i => Concluded(
                $"S{i:D2}",
                daysAgo: 10 + i,
                outcome: i % 3 == 0 ? AnalysisOutcome.Invalidated : AnalysisOutcome.TargetReached,
                f: Features(score: 0.6 + i * 0.02)))
            .ToList();

    [Test]
    public void Report_SameQueryAndCorpus_IsByteIdentical()
    {
        var query = Features(score: 0.75);
        var corpus = Corpus();

        var first = HistoricalAnalogReporter.Report(query, corpus, AsOf, k: 5);
        var second = HistoricalAnalogReporter.Report(query, corpus, AsOf, k: 5);

        Assert.That(JsonSerializer.Serialize(first, Json), Is.EqualTo(JsonSerializer.Serialize(second, Json)));
    }

    [Test]
    public void Report_AggregatesTheRetrievedAnalogs()
    {
        var report = HistoricalAnalogReporter.Report(Features(), Corpus(), AsOf, k: 8);

        Assert.Multiple(() =>
        {
            Assert.That(report.AsOf, Is.EqualTo(AsOf));
            Assert.That(report.Analogs, Has.Count.EqualTo(8));
            Assert.That(report.Stats.SampleCount, Is.EqualTo(8));
            // i%3==0 for i in 0..7 → 0,3,6 invalidated (3), the rest target-reached (5).
            Assert.That(report.Stats.Invalidated, Is.EqualTo(3));
            Assert.That(report.Stats.TargetReached, Is.EqualTo(5));
        });
    }

    [Test]
    public void Report_BelowMinimumSample_IsInsufficient()
    {
        var small = new[]
        {
            Concluded("A", 10, AnalysisOutcome.TargetReached, Features()),
            Concluded("B", 12, AnalysisOutcome.Invalidated, Features()),
        };

        var report = HistoricalAnalogReporter.Report(Features(), small, AsOf, k: 25, minimumSample: 5);

        Assert.Multiple(() =>
        {
            Assert.That(report.Stats.Sufficient, Is.False);
            Assert.That(report.Stats.SampleCount, Is.EqualTo(2));
            Assert.That(report.Narrative, Is.Null);
        });
    }
}
