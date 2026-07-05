using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CachingSymbolResolver"/>: identical queries resolve once and are
/// served from cache thereafter; distinct queries each resolve. Uses a real MemoryCache and a
/// substituted inner resolver.
/// </summary>
[TestFixture]
public sealed class CachingSymbolResolverTests
{
    private static readonly IReadOnlyList<ResolvedSymbol> Rklb =
        [new("RKLB", "Rocket Lab USA, Inc.", "EQUITY", "NASDAQ")];

    private static CachingSymbolResolver Build(ISymbolResolver inner) =>
        new(inner, new MemoryCache(new MemoryCacheOptions()), NullLogger<CachingSymbolResolver>.Instance);

    [Test]
    public async Task SearchAsync_SameQueryTwice_ResolvesInnerOnce()
    {
        var inner = Substitute.For<ISymbolResolver>();
        inner.SearchAsync("RKLB", Arg.Any<CancellationToken>()).Returns(Task.FromResult(Rklb));
        var sut = Build(inner);

        await sut.SearchAsync("RKLB");
        var second = await sut.SearchAsync("rklb"); // case-insensitive cache key

        await inner.Received(1).SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.That(second, Is.EqualTo(Rklb));
    }

    [Test]
    public async Task SearchAsync_DifferentQueries_EachResolve()
    {
        var inner = Substitute.For<ISymbolResolver>();
        inner.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ResolvedSymbol>>(Rklb));
        var sut = Build(inner);

        await sut.SearchAsync("RKLB");
        await sut.SearchAsync("AAPL");

        await inner.Received(2).SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
