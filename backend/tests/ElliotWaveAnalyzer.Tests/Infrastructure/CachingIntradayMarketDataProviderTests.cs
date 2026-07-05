using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CachingIntradayMarketDataProvider"/>: identical (symbol, days) hourly
/// requests hit the inner provider once; different days are cached separately; SupportsIntraday
/// delegates. Real MemoryCache, substituted inner provider.
/// </summary>
[TestFixture]
public sealed class CachingIntradayMarketDataProviderTests
{
    private static CachingIntradayMarketDataProvider Build(IIntradayMarketDataProvider inner) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()), NullLogger<CachingIntradayMarketDataProvider>.Instance);

    [Test]
    public async Task GetHourlyCandlesAsync_SameRequestTwice_HitsInnerOnce()
    {
        var candles = MarketDataFixtures.CreateCandles(24);
        var inner = Substitute.For<IIntradayMarketDataProvider>();
        inner.GetHourlyCandlesAsync("RKLB", 30, Arg.Any<CancellationToken>()).Returns(Task.FromResult(candles));
        var sut = Build(inner);

        await sut.GetHourlyCandlesAsync("RKLB", 30);
        await sut.GetHourlyCandlesAsync("rklb", 30); // case-insensitive key

        await inner.Received(1).GetHourlyCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetHourlyCandlesAsync_DifferentDays_AreCachedSeparately()
    {
        var inner = Substitute.For<IIntradayMarketDataProvider>();
        inner.GetHourlyCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<MarketCandle>>(MarketDataFixtures.CreateCandles(10)));
        var sut = Build(inner);

        await sut.GetHourlyCandlesAsync("RKLB", 30);
        await sut.GetHourlyCandlesAsync("RKLB", 60);

        await inner.Received(2).GetHourlyCandlesAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public void SupportsIntraday_DelegatesToInner()
    {
        var inner = Substitute.For<IIntradayMarketDataProvider>();
        inner.SupportsIntraday("RKLB").Returns(true);

        Assert.That(Build(inner).SupportsIntraday("RKLB"), Is.True);
    }
}
