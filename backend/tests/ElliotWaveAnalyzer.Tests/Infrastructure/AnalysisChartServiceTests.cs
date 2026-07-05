using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="AnalysisChartService"/> against faked collaborators: ownership is
/// delegated (a null analysis yields a null render → 404 at the endpoint), and a symbol no provider
/// supports still renders — the annotations are drawn on an empty pane rather than failing the export.
/// </summary>
[TestFixture]
public sealed class AnalysisChartServiceTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Id = Guid.NewGuid();

    private static AnalysisChartService Build(
        ITrackRecordService trackRecord, params IMarketDataProvider[] providers) =>
        new(trackRecord, providers, new SkiaAnnotatedChartRenderer(), TimeProvider.System);

    private static TrackedAnalysis Analysis(string symbol) => new(
        Id, symbol, DateTimeOffset.UnixEpoch, "Impulse", Bullish: true,
        InvalidationPrice: 30_000m, InvalidationAbove: false, TargetLow: 60_000m, TargetHigh: 65_000m,
        Confidence: "high", Score: 0.8m, AnalysisOutcome.Pending, EvaluatedPrice: null, EvaluatedAt: null);

    [Test]
    public async Task RenderChartAsync_UnknownOrOtherUsersAnalysis_ReturnsNull()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns((TrackedAnalysis?)null);

        var png = await Build(trackRecord).RenderChartAsync(User, Id);

        Assert.That(png, Is.Null);
    }

    [Test]
    public async Task RenderChartAsync_NoProviderForSymbol_StillRendersOnEmptyPane()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));

        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports(Arg.Any<string>()).Returns(false);

        var png = await Build(trackRecord, provider).RenderChartAsync(User, Id);

        Assert.That(png, Is.Not.Null);
        Assert.That(png!.Length, Is.GreaterThan(100));
    }
}
