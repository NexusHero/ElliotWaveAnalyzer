namespace ElliotWaveAnalyzer.Api.Domain.Depot;

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
