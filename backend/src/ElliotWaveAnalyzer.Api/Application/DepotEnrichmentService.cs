using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates live-price enrichment for a depot snapshot (#114): for each position missing a
/// market price, resolves its ISIN to a data-source symbol (<see cref="ISymbolResolver"/>), looks up
/// the latest price (<see cref="IQuoteProvider"/>), and applies the pure
/// <see cref="DepotPositionEnricher"/> calculation. A position already carrying a market price, or
/// whose ISIN doesn't resolve, or whose quote is unavailable, passes through unchanged — enrichment
/// never fails the whole snapshot for one unresolvable position.
/// </summary>
internal sealed class DepotEnrichmentService(
    ISymbolResolver resolver, IQuoteProvider quotes) : IDepotEnrichmentService
{
    public async Task<DepotSnapshot> EnrichAsync(DepotSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var enriched = new List<DepotPosition>(snapshot.Positions.Count);
        foreach (var position in snapshot.Positions)
        {
            if (position.MarketPrice is not null)
            {
                enriched.Add(position);
                continue;
            }

            var quote = await ResolveQuoteAsync(position.Isin, cancellationToken);
            enriched.Add(DepotPositionEnricher.Enrich(position, quote));
        }

        return snapshot with { Positions = enriched };
    }

    private async Task<decimal?> ResolveQuoteAsync(string isin, CancellationToken cancellationToken)
    {
        var matches = await resolver.SearchAsync(isin, cancellationToken);
        return matches.Count == 0 ? null : await quotes.GetLatestPriceAsync(matches[0].Symbol, cancellationToken);
    }
}
