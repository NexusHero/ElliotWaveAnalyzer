using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>Portfolio summary math over hand-built briefs: exact above/below/in-zone/unresolved counts.</summary>
[TestFixture]
public sealed class PortfolioSummaryCalculatorTests
{
    private static PositionBrief Brief(decimal price, decimal invalidation, bool inZone) => new(
        "ISIN", "SYM", "Name", "chain", Bullish: true,
        CurrentPrice: price,
        Invalidation: new PriceLevel(invalidation, LevelSide.Below, "inv", "x"),
        EntryZone: inZone ? new PriceZone(price - 1m, price + 1m, "entry", "x") : null,
        TargetZones: [],
        Scale: FibScale.Linear)
    {
        AboveInvalidation = price > invalidation,
        InEntryZone = inZone,
    };

    [Test]
    public void Summarize_CountsAboveBelowInZoneAndUnresolved()
    {
        // 2 above invalidation, 1 below, 1 of the above also in its entry zone; plus 1 unresolved.
        IReadOnlyList<PositionBrief> briefs =
        [
            Brief(price: 150m, invalidation: 120m, inZone: false),
            Brief(price: 200m, invalidation: 180m, inZone: true),
            Brief(price: 90m, invalidation: 100m, inZone: false),
        ];

        var summary = PortfolioSummaryCalculator.Summarize(briefs, unresolvedCount: 1);

        Assert.Multiple(() =>
        {
            Assert.That(summary.Positions, Is.EqualTo(4));
            Assert.That(summary.Reviewed, Is.EqualTo(3));
            Assert.That(summary.AboveInvalidation, Is.EqualTo(2));
            Assert.That(summary.BelowInvalidation, Is.EqualTo(1));
            Assert.That(summary.InEntryZone, Is.EqualTo(1));
            Assert.That(summary.Unresolved, Is.EqualTo(1));
        });
    }

    [Test]
    public void Summarize_EmptyPortfolio_IsAllZero()
    {
        var summary = PortfolioSummaryCalculator.Summarize([], unresolvedCount: 0);
        Assert.That(summary, Is.EqualTo(new PortfolioSummary(0, 0, 0, 0, 0, 0)));
    }
}
