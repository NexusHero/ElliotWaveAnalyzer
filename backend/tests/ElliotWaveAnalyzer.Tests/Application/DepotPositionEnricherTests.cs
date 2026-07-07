using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="DepotPositionEnricher"/>: pure derivation of market value/gain-loss from a resolved
/// quote — never overwrites a source-carried market price, never fails on a missing quote (#114).
/// </summary>
[TestFixture]
public sealed class DepotPositionEnricherTests
{
    private static DepotPosition Position(
        decimal? marketPrice = null, decimal? costValue = null, decimal quantity = 10m) =>
        new("US0000000001", null, "ACME Robotics", quantity, CostPrice: 100m, CostValue: costValue,
            MarketPrice: marketPrice, MarketValue: null, GainAbsolute: null, GainRelativePercent: null, Exchange: null);

    [Test]
    public void Enrich_MissingMarketPriceWithAQuote_FillsMarketPriceAndValue()
    {
        var enriched = DepotPositionEnricher.Enrich(Position(), quote: 120.00m);

        Assert.Multiple(() =>
        {
            Assert.That(enriched.MarketPrice, Is.EqualTo(120.00m));
            Assert.That(enriched.MarketValue, Is.EqualTo(1200.00m)); // 120 * 10
        });
    }

    [Test]
    public void Enrich_WithCostValue_DerivesGainAbsoluteAndRelativePercent()
    {
        var enriched = DepotPositionEnricher.Enrich(Position(costValue: 1000m), quote: 120.00m);

        Assert.Multiple(() =>
        {
            Assert.That(enriched.GainAbsolute, Is.EqualTo(200.00m)); // 1200 - 1000
            Assert.That(enriched.GainRelativePercent, Is.EqualTo(20.00m)); // 200 / 1000 * 100
        });
    }

    [Test]
    public void Enrich_WithoutCostValue_LeavesGainFieldsNull_NotFabricated()
    {
        var enriched = DepotPositionEnricher.Enrich(Position(costValue: null), quote: 120.00m);

        Assert.Multiple(() =>
        {
            Assert.That(enriched.GainAbsolute, Is.Null);
            Assert.That(enriched.GainRelativePercent, Is.Null);
        });
    }

    [Test]
    public void Enrich_PositionAlreadyHasAMarketPrice_IsReturnedUnchanged_SourceDataNeverOverwritten()
    {
        var original = Position(marketPrice: 999.00m);

        var result = DepotPositionEnricher.Enrich(original, quote: 1.00m);

        Assert.That(result, Is.EqualTo(original));
    }

    [Test]
    public void Enrich_NoQuoteResolved_ReturnsPositionUnchanged_NoCrash()
    {
        var original = Position();

        var result = DepotPositionEnricher.Enrich(original, quote: null);

        Assert.That(result, Is.EqualTo(original));
    }

    [Test]
    public void Enrich_NullPosition_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DepotPositionEnricher.Enrich(null!, 100m));
    }
}
