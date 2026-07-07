using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure computation of a position's live-derived market value and gain/loss from a resolved quote
/// (#114). Never overwrites a value the source file already carried — only fills a gap. A position
/// that already has a market price, or for which no quote resolved, is returned unchanged. No I/O;
/// the quote lookup itself is <see cref="Interfaces.IDepotEnrichmentService"/>'s job.
/// </summary>
public static class DepotPositionEnricher
{
    /// <summary>
    /// Returns <paramref name="position"/> with <c>MarketPrice</c>/<c>MarketValue</c>/gain fields
    /// filled from <paramref name="quote"/> when the position had none and a quote resolved.
    /// </summary>
    public static DepotPosition Enrich(DepotPosition position, decimal? quote)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.MarketPrice is not null || quote is not { } price)
        {
            return position;
        }

        var marketValue = price * position.Quantity;
        var gainAbsolute = position.CostValue is { } costValue ? marketValue - costValue : (decimal?)null;
        var gainRelativePercent = position.CostValue is > 0m && gainAbsolute is { } gain
            ? Math.Round(gain / position.CostValue.Value * 100m, 2)
            : (decimal?)null;

        return position with
        {
            MarketPrice = price,
            MarketValue = marketValue,
            GainAbsolute = gainAbsolute,
            GainRelativePercent = gainRelativePercent,
        };
    }
}
