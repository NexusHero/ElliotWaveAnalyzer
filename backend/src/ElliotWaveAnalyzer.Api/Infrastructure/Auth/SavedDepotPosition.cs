namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>One holding of a persisted <see cref="SavedDepot"/> (mirrors a DepotPosition).</summary>
internal sealed class SavedDepotPosition
{
    public Guid Id { get; set; }

    public Guid SavedDepotId { get; set; }

    /// <summary>
    /// Zero-based position in the imported statement. Persisted so the depot reads back in the
    /// exact order it was imported — a bag navigation has no inherent order and PostgreSQL is free
    /// to return rows in any sequence, so this is the deterministic sort key on read.
    /// </summary>
    public int Ordinal { get; set; }

    public string Isin { get; set; } = "";

    public string? Wkn { get; set; }

    public string Name { get; set; } = "";

    public decimal Quantity { get; set; }

    public decimal? CostPrice { get; set; }

    public decimal? CostValue { get; set; }

    public decimal? MarketPrice { get; set; }

    public decimal? MarketValue { get; set; }

    public decimal? GainAbsolute { get; set; }

    public decimal? GainRelativePercent { get; set; }

    public string? Exchange { get; set; }
}
