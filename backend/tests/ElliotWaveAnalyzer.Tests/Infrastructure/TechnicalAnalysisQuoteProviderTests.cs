using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="TechnicalAnalysisQuoteProvider"/>: derives "the latest price" from the last candle of
/// the existing candle pipeline, degrading to null (never throwing) when the symbol isn't supported
/// or the range is out of bounds (#114).
/// </summary>
[TestFixture]
public sealed class TechnicalAnalysisQuoteProviderTests
{
    [Test]
    public async Task GetLatestPriceAsync_ReturnsTheMostRecentCandleClose()
    {
        var technicalAnalysis = Substitute.For<ITechnicalAnalysisService>();
        IReadOnlyList<MarketCandle> candles =
        [
            new(new DateTime(2026, 1, 1), 100m, 105m, 95m, 100m, 0m),
            new(new DateTime(2026, 1, 2), 100m, 125m, 99m, 120.50m, 0m),
        ];
        technicalAnalysis.GetAnalysisAsync("ACME", 30, CandleInterval.OneDay, Arg.Any<CancellationToken>())
            .Returns(new TechnicalAnalysisResult("ACME", candles, [], []));

        var sut = new TechnicalAnalysisQuoteProvider(technicalAnalysis);
        var price = await sut.GetLatestPriceAsync("ACME");

        Assert.That(price, Is.EqualTo(120.50m));
    }

    [Test]
    public async Task GetLatestPriceAsync_NoCandles_ReturnsNull()
    {
        var technicalAnalysis = Substitute.For<ITechnicalAnalysisService>();
        technicalAnalysis.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(new TechnicalAnalysisResult("ACME", [], [], []));

        var sut = new TechnicalAnalysisQuoteProvider(technicalAnalysis);
        Assert.That(await sut.GetLatestPriceAsync("ACME"), Is.Null);
    }

    [Test]
    public async Task GetLatestPriceAsync_UnsupportedSymbol_ReturnsNull_NoCrash()
    {
        var technicalAnalysis = Substitute.For<ITechnicalAnalysisService>();
        technicalAnalysis.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TechnicalAnalysisResult>(new ArgumentException("no provider")));

        var sut = new TechnicalAnalysisQuoteProvider(technicalAnalysis);
        Assert.That(await sut.GetLatestPriceAsync("UNKNOWN"), Is.Null);
    }

    [Test]
    public async Task GetLatestPriceAsync_RangeExceeded_ReturnsNull_NoCrash()
    {
        var technicalAnalysis = Substitute.For<ITechnicalAnalysisService>();
        technicalAnalysis.GetAnalysisAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TechnicalAnalysisResult>(new MarketDataRangeException("too far back", 730)));

        var sut = new TechnicalAnalysisQuoteProvider(technicalAnalysis);
        Assert.That(await sut.GetLatestPriceAsync("ACME"), Is.Null);
    }
}
