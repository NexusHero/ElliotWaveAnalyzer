using Cronos;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Base for hosted services that run a unit of work on a cron schedule (UTC). It owns the
/// scheduling machinery shared by every cron job — parse the expression (and stop cleanly if it
/// is invalid), wait until the next occurrence, run the work in a fresh DI scope, and swallow a
/// failed run so the loop survives — leaving subclasses to supply only the three things that
/// actually differ (Template Method): the scheduler's name, its cron expression, and the work.
/// </summary>
internal abstract class CronBackgroundService(IServiceProvider services, ILogger logger)
    : BackgroundService
{
    /// <summary>Human-readable name used in log messages (e.g. "Alert scheduler").</summary>
    protected abstract string SchedulerName { get; }

    /// <summary>The cron expression to run on (UTC), read fresh from options.</summary>
    protected abstract string CronExpression { get; }

    /// <summary>Runs one scheduled occurrence inside the provided DI scope.</summary>
    protected abstract Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        CronExpression cron;
        try
        {
            cron = Cronos.CronExpression.Parse(CronExpression);
        }
        catch (CronFormatException ex)
        {
            logger.LogError(
                ex, "Invalid {Scheduler} cron '{Cron}'; scheduler not started", SchedulerName, CronExpression);
            return;
        }

        logger.LogInformation("{Scheduler} started (cron '{Cron}', UTC)", SchedulerName, CronExpression);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (next is null)
            {
                logger.LogWarning(
                    "{Scheduler} cron '{Cron}' has no future occurrences; stopping", SchedulerName, CronExpression);
                return;
            }

            var delay = next.Value - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

            using var scope = services.CreateScope();
            try
            {
                await RunOnceAsync(scope, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "{Scheduler} run failed", SchedulerName);
            }
        }
    }
}
