using Serilog;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Serilog host wiring. The bootstrap logger lives in Program.cs (it must be active
/// before DI is ready); this configures the fully-DI-aware logger for the running host.
/// </summary>
internal static class LoggingExtensions
{
    internal static WebApplicationBuilder AddAppLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, services, config) => config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());

        return builder;
    }
}
