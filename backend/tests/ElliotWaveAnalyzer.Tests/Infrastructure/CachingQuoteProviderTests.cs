using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CachingQuoteProvider"/>: identical symbols resolve once and are served
/// from cache thereafter; distinct symbols each resolve (#114, rate-limiting requirement).
/// </summary>
[TestFixture]
public sealed class CachingQuoteProviderTests
{
    private static CachingQuoteProvider Build(IQuoteProvider inner) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()), NullLogger<CachingQuoteProvider>.Instance);

    [Test]
    public async Task GetLatestPriceAsync_SameSymbolTwice_ResolvesInnerOnce()
    {
        var inner = Substitute.For<IQuoteProvider>();
        inner.GetLatestPriceAsync("RKLB", Arg.Any<CancellationToken>()).Returns(25.50m);
        var sut = Build(inner);

        await sut.GetLatestPriceAsync("RKLB");
        var second = await sut.GetLatestPriceAsync("rklb"); // case-insensitive cache key

        await inner.Received(1).GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.That(second, Is.EqualTo(25.50m));
    }

    [Test]
    public async Task GetLatestPriceAsync_DifferentSymbols_EachResolve()
    {
        var inner = Substitute.For<IQuoteProvider>();
        inner.GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(25.50m);
        var sut = Build(inner);

        await sut.GetLatestPriceAsync("RKLB");
        await sut.GetLatestPriceAsync("AAPL");

        await inner.Received(2).GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetLatestPriceAsync_UnavailableQuote_ReturnsNull_NoCrash()
    {
        var inner = Substitute.For<IQuoteProvider>();
        inner.GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<decimal?>(null));
        var sut = Build(inner);

        var result = await sut.GetLatestPriceAsync("UNKNOWN");

        Assert.That(result, Is.Null);
    }
}
