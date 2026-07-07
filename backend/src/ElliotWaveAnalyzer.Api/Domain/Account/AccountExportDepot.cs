namespace ElliotWaveAnalyzer.Api.Domain.Account;

/// <summary>One persisted depot import snapshot, including its full holdings.</summary>
public sealed record AccountExportDepot(
    Guid Id,
    string Source,
    DateTimeOffset ImportedAt,
    DateTimeOffset? ExportedAt,
    string Currency,
    decimal? TotalValue,
    decimal? GainAbsolute,
    decimal? GainRelativePercent,
    IReadOnlyList<AccountExportDepotPosition> Positions);
