using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="DepotEnrichmentService"/>: orchestrates ISIN→symbol resolution and a quote lookup per
/// position missing a market price, degrading gracefully when either step comes up empty (#114).
/// </summary>
[TestFixture]
public sealed class DepotEnrichmentServiceTests
{
    private static DepotPosition Position(string isin, decimal? marketPrice = null) =>
        new(isin, null, "ACME Robotics", 10m, CostPrice: 100m, CostValue: 1000m,
            MarketPrice: marketPrice, MarketValue: null, GainAbsolute: null, GainRelativePercent: null, Exchange: null);

    private static DepotSnapshot Snapshot(params DepotPosition[] positions) =>
        new(BrokerSource.ScalableCapital, DateTimeOffset.UtcNow, null, "EUR", positions, null);

    [Test]
    public async Task EnrichAsync_PositionMissingMarketPrice_ResolvesSymbolThenQuote_FillsMarketValue()
    {
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.SearchAsync("US0000000001", Arg.Any<CancellationToken>())
            .Returns([new ResolvedSymbol("ACME", "ACME Robotics", "EQUITY", "NASDAQ")]);
        var quotes = Substitute.For<IQuoteProvider>();
        quotes.GetLatestPriceAsync("ACME", Arg.Any<CancellationToken>()).Returns(120.00m);

        var sut = new DepotEnrichmentService(resolver, quotes);
        var result = await sut.EnrichAsync(Snapshot(Position("US0000000001")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Positions[0].MarketPrice, Is.EqualTo(120.00m));
            Assert.That(result.Positions[0].MarketValue, Is.EqualTo(1200.00m));
        });
    }

    [Test]
    public async Task EnrichAsync_PositionAlreadyHasAMarketPrice_SkipsResolutionEntirely()
    {
        var resolver = Substitute.For<ISymbolResolver>();
        var quotes = Substitute.For<IQuoteProvider>();

        var sut = new DepotEnrichmentService(resolver, quotes);
        await sut.EnrichAsync(Snapshot(Position("US0000000001", marketPrice: 999m)));

        await resolver.DidNotReceive().SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await quotes.DidNotReceive().GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnrichAsync_IsinDoesNotResolve_LeavesPositionUnchanged_NoCrash()
    {
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ResolvedSymbol>>([]));
        var quotes = Substitute.For<IQuoteProvider>();

        var sut = new DepotEnrichmentService(resolver, quotes);
        var result = await sut.EnrichAsync(Snapshot(Position("XX0000000009")));

        Assert.That(result.Positions[0].MarketPrice, Is.Null);
        await quotes.DidNotReceive().GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnrichAsync_QuoteUnavailable_LeavesPositionUnchanged_NoCrash()
    {
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.SearchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns([new ResolvedSymbol("ACME", "ACME Robotics", "EQUITY", "NASDAQ")]);
        var quotes = Substitute.For<IQuoteProvider>();
        quotes.GetLatestPriceAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<decimal?>(null));

        var sut = new DepotEnrichmentService(resolver, quotes);
        var result = await sut.EnrichAsync(Snapshot(Position("US0000000001")));

        Assert.That(result.Positions[0].MarketPrice, Is.Null);
    }

    [Test]
    public async Task EnrichAsync_MultiplePositions_EachResolvedIndependently()
    {
        var resolver = Substitute.For<ISymbolResolver>();
        resolver.SearchAsync("US0000000001", Arg.Any<CancellationToken>())
            .Returns([new ResolvedSymbol("ACME", "ACME Robotics", "EQUITY", "NASDAQ")]);
        resolver.SearchAsync("DE0000000002", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ResolvedSymbol>>([]));
        var quotes = Substitute.For<IQuoteProvider>();
        quotes.GetLatestPriceAsync("ACME", Arg.Any<CancellationToken>()).Returns(120.00m);

        var sut = new DepotEnrichmentService(resolver, quotes);
        var result = await sut.EnrichAsync(Snapshot(Position("US0000000001"), Position("DE0000000002")));

        Assert.Multiple(() =>
        {
            Assert.That(result.Positions[0].MarketPrice, Is.EqualTo(120.00m));
            Assert.That(result.Positions[1].MarketPrice, Is.Null);
        });
    }

    [Test]
    public void EnrichAsync_NullSnapshot_Throws()
    {
        var sut = new DepotEnrichmentService(Substitute.For<ISymbolResolver>(), Substitute.For<IQuoteProvider>());
        Assert.ThrowsAsync<ArgumentNullException>(() => sut.EnrichAsync(null!));
    }
}
