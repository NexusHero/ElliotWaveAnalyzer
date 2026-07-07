namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One holding of an exported depot snapshot.</summary>
public sealed record AccountExportDepotPosition(
    int Ordinal,
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
