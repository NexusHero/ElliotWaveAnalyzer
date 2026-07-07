using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="SentimentAnalysisService"/>: no-provider and empty-coverage both yield an honest
/// <see cref="SentimentReport.NoCoverage"/> (AC4); a covering provider's readings flow through
/// normalization, divergence detection and the narrator.
/// </summary>
[TestFixture]
public sealed class SentimentAnalysisServiceTests
{
    private static WaveAnnotation P(int day, decimal price, string label) =>
        new(new DateTime(2024, 1, 1).AddDays(day), price, label);

    private sealed class StubProvider(bool supports, IReadOnlyList<SentimentPoint> points) : ISentimentProvider
    {
        public bool Supports(string symbol) => supports;

        public Task<IReadOnlyList<SentimentPoint>> GetSentimentAsync(
            string symbol, int days, CancellationToken cancellationToken = default)
            => Task.FromResult(points);
    }

    private sealed class PassThroughNarrator : ISentimentNarrator
    {
        public SentimentReport? Received { get; private set; }

        public Task<SentimentReport> NarrateAsync(SentimentReport report, CancellationToken cancellationToken = default)
        {
            Received = report;
            return Task.FromResult(report);
        }
    }

    [Test]
    public async Task AnalyzeAsync_NoProviderSupportsTheSymbol_ReturnsNoCoverage()
    {
        var narrator = new PassThroughNarrator();
        var service = new SentimentAnalysisService([new StubProvider(false, [])], narrator);

        var result = await service.AnalyzeAsync("SPX", [], 180);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasCoverage, Is.False);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("No sentiment provider"));
            Assert.That(narrator.Received, Is.Null, "the narrator should never run without coverage");
        });
    }

    [Test]
    public async Task AnalyzeAsync_ProviderReturnsNoReadings_ReturnsNoCoverage()
    {
        var narrator = new PassThroughNarrator();
        var service = new SentimentAnalysisService([new StubProvider(true, [])], narrator);

        var result = await service.AnalyzeAsync("SPX", [], 180);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasCoverage, Is.False);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("no coverage"));
        });
    }

    [Test]
    public async Task AnalyzeAsync_CoveredSymbol_NormalizesAndDetectsDivergencesBeforeNarrating()
    {
        var raw = new[]
        {
            new SentimentPoint(new DateTime(2024, 1, 11), 4.0), // out of range — clamped to 1.0
            new SentimentPoint(new DateTime(2024, 1, 21), 0.2),
        };
        var pivots = new[] { P(10, 140m, "3"), P(20, 160m, "5") }; // price extends, mood doesn't confirm
        var narrator = new PassThroughNarrator();
        var service = new SentimentAnalysisService([new StubProvider(true, raw)], narrator);

        var result = await service.AnalyzeAsync("SPX", pivots, 180);

        Assert.Multiple(() =>
        {
            Assert.That(result.HasCoverage, Is.True);
            Assert.That(result.Series.Select(p => p.Score), Is.EqualTo(new[] { 1.0, 0.2 }));
            Assert.That(result.Divergences, Has.Count.EqualTo(1));
            Assert.That(result.Divergences[0].Kind, Is.EqualTo(MoodDivergenceKind.Bearish));
            Assert.That(narrator.Received, Is.Not.Null, "the narrator should run once coverage exists");
        });
    }

    [Test]
    public async Task AnalyzeAsync_MultipleProviders_UsesTheFirstThatSupportsTheSymbol()
    {
        var unsupporting = new StubProvider(false, [new SentimentPoint(new DateTime(2024, 1, 1), 0.5)]);
        var supporting = new StubProvider(true, [new SentimentPoint(new DateTime(2024, 1, 1), 0.5)]);
        var service = new SentimentAnalysisService([unsupporting, supporting], new PassThroughNarrator());

        var result = await service.AnalyzeAsync("SPX", [], 180);

        Assert.That(result.HasCoverage, Is.True);
    }
}
