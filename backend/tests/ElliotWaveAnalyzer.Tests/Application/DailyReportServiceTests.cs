using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="DailyReportService"/> — orchestration and failure isolation.
/// </summary>
[TestFixture]
public sealed class DailyReportServiceTests
{
    private ITechnicalAnalysisService _analysis = null!;
    private IChartRenderer _renderer = null!;

    [SetUp]
    public void SetUp()
    {
        _analysis = Substitute.For<ITechnicalAnalysisService>();
        _renderer = Substitute.For<IChartRenderer>();

        _analysis.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(new TechnicalAnalysisResult(
                (string)ci[0], MarketDataFixtures.CreateCandles(30), [], [])));
        _renderer.RenderPng(Arg.Any<TechnicalAnalysisResult>()).Returns([1, 2, 3]);
    }

    private static IReportDeliveryChannel Channel(string name, bool enabled)
    {
        var channel = Substitute.For<IReportDeliveryChannel>();
        channel.Name.Returns(name);
        channel.IsEnabled.Returns(enabled);
        return channel;
    }

    private DailyReportService BuildSut(IEnumerable<IReportDeliveryChannel> channels, params string[] symbols) =>
        new(
            _analysis,
            _renderer,
            channels,
            Options.Create(new DailyReportOptions { Symbols = symbols, Days = 90 }),
            NullLogger<DailyReportService>.Instance);

    [Test]
    public async Task RunAsync_DeliversEverySymbolThroughEnabledChannels()
    {
        var enabled = Channel("Telegram", enabled: true);
        var disabled = Channel("Email", enabled: false);
        var sut = BuildSut([enabled, disabled], "BTC", "ETH");

        await sut.RunAsync();

        _renderer.Received(2).RenderPng(Arg.Any<TechnicalAnalysisResult>());
        await enabled.Received(2).SendAsync(Arg.Any<ReportArtifact>(), Arg.Any<CancellationToken>());
        await disabled.DidNotReceive().SendAsync(Arg.Any<ReportArtifact>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_NoEnabledChannels_DoesNothing()
    {
        var sut = BuildSut([Channel("Email", enabled: false)], "BTC");

        await sut.RunAsync();

        _renderer.DidNotReceive().RenderPng(Arg.Any<TechnicalAnalysisResult>());
    }

    [Test]
    public async Task RunAsync_OneChannelThrows_OtherChannelStillReceives()
    {
        var failing = Channel("Telegram", enabled: true);
        failing.SendAsync(Arg.Any<ReportArtifact>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("boom")));
        var working = Channel("Email", enabled: true);

        var sut = BuildSut([failing, working], "BTC");

        await sut.RunAsync();

        await working.Received(1).SendAsync(Arg.Any<ReportArtifact>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RunAsync_OneSymbolFails_OtherSymbolStillDelivered()
    {
        _analysis.GetAnalysisAsync("BTC", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TechnicalAnalysisResult>(new InvalidOperationException("no data")));
        var channel = Channel("Telegram", enabled: true);

        var sut = BuildSut([channel], "BTC", "ETH");

        await sut.RunAsync();

        // BTC failed, ETH still delivered → exactly one send.
        await channel.Received(1).SendAsync(Arg.Any<ReportArtifact>(), Arg.Any<CancellationToken>());
    }
}
