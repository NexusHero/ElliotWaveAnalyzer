using ElliotWaveAnalyzer.Api.Application.Charting;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;
using NSubstitute;
using SkiaSharp;

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

    // ── #227: publishing size, theme, watermark and determinism, at the assembled-PNG level ──────

    [Test]
    public async Task RenderChartAsync_DefaultOptions_MeetsThePublishingSizeMinimum()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));
        var provider = NoOpProvider();

        var png = await Build(trackRecord, provider).RenderChartAsync(User, Id);

        using var bitmap = SKBitmap.Decode(png);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Width, Is.EqualTo(1920));
            Assert.That(bitmap.Height, Is.EqualTo(1080));
        });
    }

    [Test]
    public async Task RenderChartAsync_Scale2x_DoublesThePngDimensions()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));
        var provider = NoOpProvider();

        var png = await Build(trackRecord, provider).RenderChartAsync(User, Id, scale2x: true);

        using var bitmap = SKBitmap.Decode(png);
        Assert.Multiple(() =>
        {
            Assert.That(bitmap.Width, Is.EqualTo(3840));
            Assert.That(bitmap.Height, Is.EqualTo(2160));
        });
    }

    [Test]
    public async Task RenderChartAsync_LightTheme_ProducesALightBackgroundPixel()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));
        var provider = NoOpProvider();

        var png = await Build(trackRecord, provider).RenderChartAsync(User, Id, ChartTheme.Light);

        using var bitmap = SKBitmap.Decode(png);
        var corner = bitmap.GetPixel(2, 2); // outside the plot area — pure background colour
        var expected = ChartPalette.Light.Background;
        Assert.Multiple(() =>
        {
            Assert.That(corner.Red, Is.EqualTo(expected.R));
            Assert.That(corner.Green, Is.EqualTo(expected.G));
            Assert.That(corner.Blue, Is.EqualTo(expected.B));
        });
    }

    [Test]
    public async Task RenderChartAsync_SameAnalysisSameOptionsSameClock_IsByteIdentical()
    {
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));
        var provider = NoOpProvider();

        var fixedClock = Substitute.For<TimeProvider>();
        fixedClock.GetUtcNow().Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var sut = new AnalysisChartService(trackRecord, [provider], new SkiaAnnotatedChartRenderer(), fixedClock);

        var first = await sut.RenderChartAsync(User, Id, ChartTheme.Light, watermarkText: "demo");
        var second = await sut.RenderChartAsync(User, Id, ChartTheme.Light, watermarkText: "demo");

        Assert.That(first, Is.EqualTo(second), "#227 AC3 — same analysis + options must byte-match");
    }

    [Test]
    public async Task RenderChartAsync_AlternateScenario_IsWiredIntoTheExport()
    {
        // A saved analysis has no stored pivots, so it cannot reconstruct a live forward projection —
        // but its persisted alternates ARE real data (ADR-072); this proves BuildInput actually reads
        // them rather than silently ignoring the scenario tree beyond the primary.
        var withAlternate = Analysis("XYZ") with
        {
            Scenarios =
            [
                new Scenario(
                    ScenarioRole.Alternate, "Alt 1", "Zigzag", Bullish: false,
                    InvalidationPrice: null, InvalidationAbove: false,
                    EntryLow: 40_000m, EntryHigh: 41_000m, TargetLow: 32_000m, TargetHigh: 33_000m,
                    Confidence: "low", Score: 0.4m, Probability: null,
                    ProbabilityBasis: ProbabilityBasis.InsufficientData, Retired: false),
            ],
        };
        var trackRecord = Substitute.For<ITrackRecordService>();
        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(withAlternate);
        var withAlternatePng = await Build(trackRecord, NoOpProvider()).RenderChartAsync(User, Id);

        trackRecord.GetAsync(User, Id, Arg.Any<CancellationToken>()).Returns(Analysis("XYZ"));
        var baselinePng = await Build(trackRecord, NoOpProvider()).RenderChartAsync(User, Id);

        // Rendering the alternate's zone draws extra ops (translucent rect + price/label text), so the
        // PNG with the alternate present must differ in byte length from the one without it.
        Assert.That(withAlternatePng!.Length, Is.Not.EqualTo(baselinePng!.Length));
    }

    private static IMarketDataProvider NoOpProvider()
    {
        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports(Arg.Any<string>()).Returns(false);
        return provider;
    }
}
