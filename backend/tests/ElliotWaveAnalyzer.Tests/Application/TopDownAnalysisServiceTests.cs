using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="TopDownAnalysisService"/>: it fetches candles per ladder rung via a
/// substituted <see cref="ITechnicalAnalysisService"/>, detects pivots, and delegates to the pure
/// analyzer — skipping any timeframe the instrument can't serve rather than failing outright.
/// </summary>
[TestFixture]
public sealed class TopDownAnalysisServiceTests
{
    private static TechnicalAnalysisResult Result(string symbol)
        => new(symbol, MarketDataFixtures.CreateCandles(400), [], []);

    private static ITechnicalAnalysisService AllTimeframes()
    {
        var tas = Substitute.For<ITechnicalAnalysisService>();
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result("AAPL")));
        return tas;
    }

    private static TopDownAnalysisService Service(ITechnicalAnalysisService tas)
        => new(tas, NullLogger<TopDownAnalysisService>.Instance);

    [Test]
    public async Task AnalyzeAsync_AllTimeframesServed_ReturnsThreeRungChain()
    {
        var result = await Service(AllTimeframes()).AnalyzeAsync("AAPL", 3m);

        Assert.Multiple(() =>
        {
            Assert.That(result.Timeframes.Select(t => t.Interval), Is.EqualTo(new[] { "1W", "1D", "4H" }));
            Assert.That(result.Summary, Does.Contain("→"));
        });
    }

    [Test]
    public async Task AnalyzeAsync_IntradayUnavailable_SkipsThatTimeframe()
    {
        var tas = Substitute.For<ITechnicalAnalysisService>();
        // Daily/weekly served; 4H (FourHours) has no intraday source → ArgumentException.
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), CandleInterval.FourHours, Arg.Any<CancellationToken>())
            .Returns<Task<TechnicalAnalysisResult>>(_ => throw new ArgumentException("no intraday"));
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), CandleInterval.OneWeek, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result("AAPL")));
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), CandleInterval.OneDay, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result("AAPL")));

        var result = await Service(tas).AnalyzeAsync("AAPL", 3m);

        Assert.That(result.Timeframes.Select(t => t.Interval), Is.EqualTo(new[] { "1W", "1D" }));
    }

    [Test]
    public async Task AnalyzeAsync_RangeExceeded_SkipsThatTimeframe()
    {
        var tas = Substitute.For<ITechnicalAnalysisService>();
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result("AAPL")));
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), CandleInterval.FourHours, Arg.Any<CancellationToken>())
            .Returns<Task<TechnicalAnalysisResult>>(_ => throw new MarketDataRangeException("too far", 730));

        var result = await Service(tas).AnalyzeAsync("AAPL", 3m);

        Assert.That(result.Timeframes, Has.Count.EqualTo(2));
    }

    [Test]
    public void AnalyzeAsync_NoTimeframeServed_Throws()
    {
        var tas = Substitute.For<ITechnicalAnalysisService>();
        tas.GetAnalysisAsync("AAPL", Arg.Any<int>(), Arg.Any<CandleInterval>(), Arg.Any<CancellationToken>())
            .Returns<Task<TechnicalAnalysisResult>>(_ => throw new ArgumentException("unsupported"));

        Assert.ThrowsAsync<ArgumentException>(
            async () => await Service(tas).AnalyzeAsync("AAPL", 3m));
    }
}
