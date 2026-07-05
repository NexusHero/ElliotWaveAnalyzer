using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

/// <summary>
/// Triggers <see cref="IDailyReportService"/> on a cron schedule. Only registered when
/// <c>DailyReport:Enabled</c> is true. The scheduling loop lives in
/// <see cref="CronBackgroundService"/>; this class supplies the schedule and the per-occurrence
/// work in its own DI scope so scoped dependencies resolve correctly.
/// </summary>
internal sealed class DailyReportBackgroundService(
    IServiceProvider services,
    IOptions<DailyReportOptions> options,
    ILogger<DailyReportBackgroundService> logger) : CronBackgroundService(services, logger)
{
    protected override string SchedulerName => "Daily report scheduler";

    protected override string CronExpression => options.Value.Cron;

    protected override Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken)
        => scope.ServiceProvider.GetRequiredService<IDailyReportService>().RunAsync(cancellationToken);
}
