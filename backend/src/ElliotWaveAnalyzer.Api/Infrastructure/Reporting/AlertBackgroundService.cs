using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Runs <see cref="IAlertService"/> on a cron schedule. Only registered when
/// <c>Alerts:Enabled</c> is true. The scheduling loop lives in <see cref="CronBackgroundService"/>;
/// this class supplies the schedule and the per-occurrence work (re-evaluate saved analyses and
/// notify) in its own DI scope so the scoped <see cref="IAlertService"/> resolves correctly.
/// </summary>
internal sealed class AlertBackgroundService(
    IServiceProvider services,
    IOptions<AlertOptions> options,
    ILogger<AlertBackgroundService> logger) : CronBackgroundService(services, logger)
{
    protected override string SchedulerName => "Alert scheduler";

    protected override string CronExpression => options.Value.Cron;

    protected override Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken)
        => scope.ServiceProvider.GetRequiredService<IAlertService>().RunAsync(cancellationToken);
}
