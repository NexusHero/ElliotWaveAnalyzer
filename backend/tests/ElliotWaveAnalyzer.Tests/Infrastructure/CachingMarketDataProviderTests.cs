using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="CachingMarketDataProvider"/> decorator: identical (symbol, days)
/// requests hit the wrapped provider once, and <see cref="IMarketDataProvider.Supports"/> delegates.
/// </summary>
[TestFixture]
public sealed class CachingMarketDataProviderTests
{
    private static readonly IReadOnlyList<MarketCandle> Candles =
        [new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100m, 110m, 95m, 105m, 0m)];

    private static CachingMarketDataProvider Build(IMarketDataProvider inner) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()), NullLogger<CachingMarketDataProvider>.Instance);

    [Test]
    public async Task GetCandlesAsync_SameRequestTwice_HitsInnerOnce()
    {
        var inner = Substitute.For<IMarketDataProvider>();
        inner.GetCandlesAsync("BTC", 30, Arg.Any<CancellationToken>()).Returns(Candles);
        var sut = Build(inner);

        var first = await sut.GetCandlesAsync("BTC", 30);
        var second = await sut.GetCandlesAsync("BTC", 30);

        Assert.Multiple(async () =>
        {
            Assert.That(first, Is.EqualTo(Candles));
            Assert.That(second, Is.EqualTo(Candles));
            await inner.Received(1).GetCandlesAsync("BTC", 30, Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task GetCandlesAsync_DifferentDays_AreCachedSeparately()
    {
        var inner = Substitute.For<IMarketDataProvider>();
        inner.GetCandlesAsync("BTC", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(Candles);
        var sut = Build(inner);

        await sut.GetCandlesAsync("BTC", 30);
        await sut.GetCandlesAsync("BTC", 90);

        await inner.Received(1).GetCandlesAsync("BTC", 30, Arg.Any<CancellationToken>());
        await inner.Received(1).GetCandlesAsync("BTC", 90, Arg.Any<CancellationToken>());
    }

    [Test]
    public void Supports_DelegatesToInner()
    {
        var inner = Substitute.For<IMarketDataProvider>();
        inner.Supports("BTC").Returns(true);

        Assert.That(Build(inner).Supports("BTC"), Is.True);
    }
}
