using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// A single persisted depot snapshot for one user — every import accumulates as one of these (#115),
/// never overwritten. Header fields mirror <see cref="DepotSnapshot"/>; the holdings live in
/// <see cref="Positions"/>.
/// </summary>
internal sealed class SavedDepot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public BrokerSource Source { get; set; }

    public DateTimeOffset ImportedAt { get; set; }

    public DateTimeOffset? ExportedAt { get; set; }

    public string Currency { get; set; } = "EUR";

    public decimal? TotalValue { get; set; }

    public decimal? GainAbsolute { get; set; }

    public decimal? GainRelativePercent { get; set; }

    public List<SavedDepotPosition> Positions { get; set; } = [];
}
