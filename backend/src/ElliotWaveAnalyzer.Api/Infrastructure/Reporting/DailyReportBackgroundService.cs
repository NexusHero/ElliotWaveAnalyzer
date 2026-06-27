using Cronos;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Hosted service that triggers <see cref="IDailyReportService"/> on a cron schedule.
/// Only registered when <c>DailyReport:Enabled</c> is true. Each run is executed in its
/// own DI scope so scoped dependencies resolve correctly.
/// </summary>
internal sealed class DailyReportBackgroundService(
    IServiceProvider services,
    IOptions<DailyReportOptions> options,
    ILogger<DailyReportBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        CronExpression cron;
        try
        {
            cron = CronExpression.Parse(opts.Cron);
        }
        catch (CronFormatException ex)
        {
            logger.LogError(ex, "Invalid DailyReport:Cron expression '{Cron}'; scheduler not started", opts.Cron);
            return;
        }

        logger.LogInformation("Daily report scheduler started (cron '{Cron}', UTC)", opts.Cron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (next is null)
            {
                logger.LogWarning("Cron '{Cron}' has no future occurrences; scheduler stopping", opts.Cron);
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
            var reportService = scope.ServiceProvider.GetRequiredService<IDailyReportService>();
            try
            {
                await reportService.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Daily report run failed");
            }
        }
    }
}
