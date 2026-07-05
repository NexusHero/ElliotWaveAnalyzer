namespace ElliotWaveAnalyzer.Api.Domain.Depot;

/// <summary>Depot-level totals as printed on the statement (all optional).</summary>
/// <param name="TotalValue">Depotwert gesamt.</param>
/// <param name="GainAbsolute">Gewinn absolut gesamt.</param>
/// <param name="GainRelativePercent">Gewinn relativ gesamt.</param>
public sealed record DepotTotals(
    decimal? TotalValue,
    decimal? GainAbsolute,
    decimal? GainRelativePercent);
