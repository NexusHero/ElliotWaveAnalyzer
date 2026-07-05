using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Reviews a user's imported depot: resolves each position's ISIN, runs the deterministic top-down
/// analysis, derives the scenario geometry, and (optionally) narrates it — producing one brief per
/// resolvable position plus a portfolio-level summary and an explicit list of positions that could
/// not be reviewed. The differentiator: a professional-style portfolio review, automatically.
/// </summary>
public interface IPortfolioReviewService
{
    /// <summary>
    /// Builds the portfolio review for <paramref name="userId"/>'s current depot. Returns an empty
    /// review (no briefs, zeroed summary) when the user has no imported depot.
    /// </summary>
    Task<PortfolioReview> ReviewAsync(Guid userId, CancellationToken cancellationToken = default);
}
