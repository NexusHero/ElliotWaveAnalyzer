namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>
/// One row of a user's depot import history (#115) — headline metadata only, not the full position
/// list (that's the enriched <see cref="DepotSnapshot"/>, fetched by <see cref="Id"/> via
/// <c>GET /api/depot/history/{id}</c>). Listed newest first.
/// </summary>
/// <param name="Id">The saved snapshot's stable id — pass to the fetch-by-id endpoint.</param>
/// <param name="Source">Which broker this import came from.</param>
/// <param name="ImportedAt">When this snapshot was imported.</param>
/// <param name="ExportedAt">When the source statement itself was exported, if known.</param>
/// <param name="Currency">The depot's currency.</param>
/// <param name="Totals">Depot-level totals as printed on the statement, if any.</param>
public sealed record DepotHistoryEntry(
    Guid Id,
    BrokerSource Source,
    DateTimeOffset ImportedAt,
    DateTimeOffset? ExportedAt,
    string Currency,
    DepotTotals? Totals);
