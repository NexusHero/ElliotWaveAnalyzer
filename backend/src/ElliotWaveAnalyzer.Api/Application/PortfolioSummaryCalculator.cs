using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Aggregates per-position briefs into a portfolio-level risk read: how many trade above vs below
/// their invalidation, how many sit inside their entry zone, and how many positions could not be
/// reviewed. Pure and static — a deterministic function of the briefs and the unresolved list.
/// </summary>
public static class PortfolioSummaryCalculator
{
    /// <summary>Summarizes <paramref name="briefs"/> plus the count of positions that could not be reviewed.</summary>
    public static PortfolioSummary Summarize(
        IReadOnlyList<PositionBrief> briefs, int unresolvedCount)
    {
        ArgumentNullException.ThrowIfNull(briefs);

        var above = briefs.Count(b => b is { CurrentPrice: not null, Invalidation: not null } && b.AboveInvalidation);
        var below = briefs.Count(b => b is { CurrentPrice: not null, Invalidation: not null } && !b.AboveInvalidation);
        var inZone = briefs.Count(b => b.InEntryZone);

        return new PortfolioSummary(
            Positions: briefs.Count + unresolvedCount,
            Reviewed: briefs.Count,
            AboveInvalidation: above,
            BelowInvalidation: below,
            InEntryZone: inZone,
            Unresolved: unresolvedCount);
    }
}
