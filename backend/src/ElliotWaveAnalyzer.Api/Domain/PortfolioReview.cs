namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A full portfolio review: one <see cref="PositionBrief"/> per resolvable depot position, the
/// positions that could not be reviewed (with reasons), and a portfolio-level summary. Produced by
/// <see cref="Application.PortfolioReviewService"/>.
/// </summary>
/// <param name="Briefs">Per-position briefs, in depot order.</param>
/// <param name="Unresolved">Positions that could not be reviewed.</param>
/// <param name="Summary">Portfolio-level aggregation.</param>
public sealed record PortfolioReview(
    IReadOnlyList<PositionBrief> Briefs,
    IReadOnlyList<UnresolvedPosition> Unresolved,
    PortfolioSummary Summary);
