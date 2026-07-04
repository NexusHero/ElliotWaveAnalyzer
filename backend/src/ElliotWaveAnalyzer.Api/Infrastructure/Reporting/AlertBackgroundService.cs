using Cronos;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Hosted service that runs <see cref="IAlertService"/> on a cron schedule. Only registered when
/// <c>Alerts:Enabled</c> is true. Each run executes in its own DI scope so the scoped
/// <see cref="IAlertService"/> (and its <c>AppDbContext</c>) resolve correctly. Mirrors
/// <see cref="DailyReportBackgroundService"/>.
/// </summary>
internal sealed class AlertBackgroundService(
    IServiceProvider services,
    IOptions<AlertOptions> options,
    ILogger<AlertBackgroundService> logger) : BackgroundService
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
            logger.LogError(ex, "Invalid Alerts:Cron expression '{Cron}'; alert scheduler not started", opts.Cron);
            return;
        }

        logger.LogInformation("Alert scheduler started (cron '{Cron}', UTC)", opts.Cron);

        while (!stoppingToken.IsCancellationRequested)
        {
            var next = cron.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
            if (next is null)
            {
                logger.LogWarning("Cron '{Cron}' has no future occurrences; alert scheduler stopping", opts.Cron);
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
            var alertService = scope.ServiceProvider.GetRequiredService<IAlertService>();
            try
            {
                await alertService.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alert run failed");
            }
        }
    }
}
