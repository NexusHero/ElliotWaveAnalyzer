using ElliotWaveAnalyzer.Api.Infrastructure.Health;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Liveness/readiness (#173): `/health/live` reports the process is up, with no dependency checks
/// (a dependency outage must never make the process itself look dead and get killed by an
/// orchestrator). `/health/ready` runs every check tagged "ready" — the database and the market-data
/// provider — and is what a load balancer should actually gate traffic on.
/// </summary>
internal static class HealthCheckExtensions
{
    private const string ReadyTag = "ready";

    internal static IServiceCollection AddAppHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag])
            .AddCheck<MarketDataHealthCheck>("market-data", tags: [ReadyTag]);

        services.AddSingleton<IAlertSink, LoggingAlertSink>();

        services.AddOptions<HealthMonitorOptions>()
            .BindConfiguration(HealthMonitorOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{HealthMonitorOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<HealthMonitorBackgroundService>();
        }

        return services;
    }

    internal static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
            .WithTags("Health")
            .AllowAnonymous();

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReport,
        })
            .WithTags("Health")
            .AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Per-check breakdown (name, status, description) so an operator looking at a 503 knows which
    /// dependency failed and why, not just that something did. Never includes the exception's stack
    /// trace — <see cref="HealthReportEntry.Description"/> is the human-authored message each check
    /// already sets on failure (e.g. "Database is unreachable."), not the raw exception detail.
    /// </summary>
    private static Task WriteReport(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            }),
        });
    }
}
