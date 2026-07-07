using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Health;

/// <summary>
/// Polls the same readiness checks the HTTP endpoint exposes and alerts once a failure has been
/// sustained for <see cref="HealthMonitorOptions.ConsecutiveFailureThreshold"/> consecutive polls
/// (#173 AC3) — a single blip during, say, an upstream provider hiccup must not page anyone. The
/// consecutive-failure count lives in <see cref="_detector"/>, an instance field on this singleton
/// hosted service, so it persists across polling cycles for the lifetime of the process.
/// </summary>
internal sealed class HealthMonitorBackgroundService(
    IServiceProvider services, ILogger<HealthMonitorBackgroundService> logger, IOptions<HealthMonitorOptions> options)
    : CronBackgroundService(services, logger)
{
    private readonly SustainedFailureDetector _detector = new(options.Value.ConsecutiveFailureThreshold);

    protected override string SchedulerName => "Health monitor";

    protected override string CronExpression => options.Value.Cron;

    protected override async Task RunOnceAsync(IServiceScope scope, CancellationToken cancellationToken)
    {
        var healthChecks = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var report = await healthChecks.CheckHealthAsync(check => check.Tags.Contains("ready"), cancellationToken);

        if (!_detector.RecordAndShouldAlert(report.Status != HealthStatus.Healthy))
        {
            return;
        }

        var failing = string.Join(
            ", ", report.Entries.Where(e => e.Value.Status != HealthStatus.Healthy).Select(e => e.Key));
        var sink = scope.ServiceProvider.GetRequiredService<IAlertSink>();
        await sink.SendAsync($"Readiness has been failing for {failing} for {options.Value.ConsecutiveFailureThreshold} consecutive checks.", cancellationToken);
    }
}
