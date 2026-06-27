using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// OpenTelemetry traces and metrics. Instrumentation (ASP.NET Core, outbound HTTP, runtime)
/// is always active; data is exported over OTLP only when <c>OpenTelemetry:Endpoint</c> is
/// configured, so dev and test runs stay quiet with negligible overhead.
/// Microsoft.Extensions.AI auto-instruments IChatClient calls when tracing is active.
/// </summary>
internal static class TelemetryExtensions
{
    internal static IServiceCollection AddAppTelemetry(
        this IServiceCollection services, IConfiguration configuration)
    {
        var otlpEndpoint = configuration["OpenTelemetry:Endpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(
                serviceName: "ElliotWaveAnalyzer",
                serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                }
            });

        return services;
    }
}
