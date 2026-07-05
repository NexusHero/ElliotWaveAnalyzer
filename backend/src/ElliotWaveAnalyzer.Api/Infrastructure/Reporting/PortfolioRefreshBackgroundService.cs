using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Warms the portfolio review for every user who has imported a depot, on a cron schedule. Only
/// registered when <c>PortfolioReview:Enabled</c> is true. The scheduling loop lives in
/// <see cref="CronBackgroundService"/>; this class supplies the schedule and the per-occurrence work
/// (run each user's review so the per-position cache is fresh) in its own DI scope so the scoped
/// review service resolves correctly. A failed single user does not abort the pass.
/// </summary>
internal sealed class PortfolioRefreshBackgroundService(
    IServiceProvider services,
    IOptions<PortfolioReviewOptions> options,
    ILogger<PortfolioRefreshBackgroundService> logger) : CronBackgroundService(services, logger)
{
    protected override string SchedulerName => "Portfolio-review scheduler";

    protected override string CronExpression => options.Value.Cron;

    protected override async Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = scope.ServiceProvider.GetRequiredService<IPortfolioReviewService>();

        var userIds = await db.SavedDepots
            .AsNoTracking()
            .Select(d => d.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var userId in userIds)
        {
            try
            {
                await review.ReviewAsync(userId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Portfolio refresh failed for user {UserId}; continuing", userId);
            }
        }
    }
}
