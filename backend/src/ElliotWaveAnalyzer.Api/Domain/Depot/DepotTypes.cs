namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>The broker a depot snapshot was imported from.</summary>
public enum BrokerSource
{
    /// <summary>Smartbroker+ (PDF "Depotübersicht" export).</summary>
    SmartbrokerPlus,

    /// <summary>Scalable Capital (CSV export).</summary>
    ScalableCapital,

    /// <summary>Trade Republic (document export).</summary>
    TradeRepublic,
}

/// <summary>
/// One holding in a depot. Prices/values are optional because not every broker export carries
/// every field; <see cref="Isin"/>, <see cref="Name"/> and <see cref="Quantity"/> are the
/// minimum a position must have to be useful for portfolio analysis. Monetary amounts are in the
/// snapshot's <see cref="DepotSnapshot.Currency"/>.
/// </summary>
/// <param name="Isin">International Securities Identification Number (12 chars).</param>
/// <param name="Wkn">German Wertpapierkennnummer, if the export lists it separately.</param>
/// <param name="Name">Instrument name as printed on the statement.</param>
/// <param name="Quantity">Number of units held.</param>
/// <param name="CostPrice">Average purchase price per unit (Einstandskurs).</param>
/// <param name="CostValue">Total cost basis (Einstandswert).</param>
/// <param name="MarketPrice">Current price per unit (Marktkurs).</param>
/// <param name="MarketValue">Current market value of the holding (Marktwert).</param>
/// <param name="GainAbsolute">Unrealised gain/loss in currency (G/V absolut).</param>
/// <param name="GainRelativePercent">Unrealised gain/loss in percent (G/V prozentual).</param>
/// <param name="Exchange">Trading venue the position is booked on (Börse).</param>
public sealed record DepotPosition(
    string Isin,
    string? Wkn,
    string Name,
    decimal Quantity,
    decimal? CostPrice,
    decimal? CostValue,
    decimal? MarketPrice,
    decimal? MarketValue,
    decimal? GainAbsolute,
    decimal? GainRelativePercent,
    string? Exchange);

/// <summary>Depot-level totals as printed on the statement (all optional).</summary>
/// <param name="TotalValue">Depotwert gesamt.</param>
/// <param name="GainAbsolute">Gewinn absolut gesamt.</param>
/// <param name="GainRelativePercent">Gewinn relativ gesamt.</param>
public sealed record DepotTotals(
    decimal? TotalValue,
    decimal? GainAbsolute,
    decimal? GainRelativePercent);

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

/// <summary>
/// The raw uploaded file to import: its bytes plus the metadata importers sniff on
/// (<see cref="FileName"/>, <see cref="ContentType"/>).
/// </summary>
public sealed record DepotImportFile(string FileName, string ContentType, byte[] Content);

/// <summary>
/// Outcome of an import attempt — either a parsed <see cref="Snapshot"/> or an <see cref="Error"/>
/// describing why it could not be parsed (unsupported file, wrong broker, malformed content).
/// </summary>
public sealed record DepotImportResult(bool Success, DepotSnapshot? Snapshot, string? Error)
{
    public static DepotImportResult Ok(DepotSnapshot snapshot) => new(true, snapshot, null);

    public static DepotImportResult Fail(string error) => new(false, null, error);
}
