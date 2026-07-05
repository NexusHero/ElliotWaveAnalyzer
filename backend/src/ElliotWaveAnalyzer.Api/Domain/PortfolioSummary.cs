namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Portfolio-level aggregation across the reviewed positions: how many trade above their invalidation
/// line, how many below, how many sit inside their entry zone, and how many positions could not be
/// resolved. A quick risk read over the whole depot.
/// </summary>
/// <param name="Positions">Total positions in the depot (reviewed + unresolved).</param>
/// <param name="Reviewed">Positions that produced a brief.</param>
/// <param name="AboveInvalidation">Reviewed positions trading above their invalidation line.</param>
/// <param name="BelowInvalidation">Reviewed positions trading below their invalidation line.</param>
/// <param name="InEntryZone">Reviewed positions currently inside their entry zone.</param>
/// <param name="Unresolved">Positions that could not be reviewed.</param>
public sealed record PortfolioSummary(
    int Positions,
    int Reviewed,
    int AboveInvalidation,
    int BelowInvalidation,
    int InEntryZone,
    int Unresolved);
