using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Fills in a live market price (and derived market value / gain-loss) for any position in a
/// <see cref="DepotSnapshot"/> that arrived without one — e.g. from a transactions-ledger importer
/// like Scalable Capital (#114). A position that already carries a source market price is returned
/// unchanged; a position whose quote can't be resolved is also returned unchanged (degrade
/// gracefully, never throw).
/// </summary>
public interface IDepotEnrichmentService
{
    /// <summary>Returns a copy of <paramref name="snapshot"/> with missing market prices filled in where resolvable.</summary>
    Task<DepotSnapshot> EnrichAsync(DepotSnapshot snapshot, CancellationToken cancellationToken = default);
}
