namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Filters applied to a scan. All are optional; an unset filter matches everything. Keeping this a
/// value object lets the pure ranking/filtering be unit-tested without the service.
/// </summary>
/// <param name="Structure">Keep only hits of this structure kind (case-insensitive); null = any.</param>
/// <param name="MinScore">Keep only hits with at least this guideline score; null = any.</param>
/// <param name="InZoneOnly">When true, keep only hits where price is inside an entry or confluence zone.</param>
public sealed record ScanFilter(string? Structure = null, decimal? MinScore = null, bool InZoneOnly = false)
{
    /// <summary>True when <paramref name="hit"/> passes every set filter.</summary>
    public bool Matches(ScanHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (Structure is not null && !string.Equals(hit.Structure, Structure, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (MinScore is { } min && hit.Score < min)
        {
            return false;
        }

        return !InZoneOnly || hit.InEntryZone || hit.InConfluenceZone;
    }
}
