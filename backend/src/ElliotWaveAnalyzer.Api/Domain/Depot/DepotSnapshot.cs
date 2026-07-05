namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>
/// A parsed depot snapshot: the holdings plus depot-level totals, tagged with its source broker
/// and the timestamps (when the broker exported it, when we imported it).
/// </summary>
/// <param name="Source">Which broker the file came from.</param>
/// <param name="ImportedAt">When the server parsed the file.</param>
/// <param name="ExportedAt">The export timestamp printed in the file, if present.</param>
/// <param name="Currency">ISO currency of the monetary amounts (e.g. "EUR").</param>
/// <param name="Positions">The holdings, in statement order.</param>
/// <param name="Totals">Depot-level totals, if the file carries them.</param>
public sealed record DepotSnapshot(
    BrokerSource Source,
    DateTimeOffset ImportedAt,
    DateTimeOffset? ExportedAt,
    string Currency,
    IReadOnlyList<DepotPosition> Positions,
    DepotTotals? Totals);
