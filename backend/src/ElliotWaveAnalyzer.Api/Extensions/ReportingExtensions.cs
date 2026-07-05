using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Daily report (opt-in via DailyReport:Enabled). Renders a chart per symbol and delivers
/// it through every enabled channel. Adding a channel = new IReportDeliveryChannel + one
/// line here (OCP).
/// </summary>
internal static class ReportingExtensions
{
    internal static IServiceCollection AddReportingServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DailyReportOptions>()
            .BindConfiguration(DailyReportOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IChartRenderer, SkiaSharpChartRenderer>();

        // Annotated-chart export (issue #120): the SkiaSharp draw-op backend is stateless (singleton);
        // the assembling service is scoped because it resolves the scoped track-record service.
        services.AddSingleton<IAnnotatedChartRenderer, SkiaAnnotatedChartRenderer>();
        services.AddScoped<IAnalysisChartService, AnalysisChartService>();

        services.AddHttpClient<TelegramDeliveryChannel>(client =>
            client.BaseAddress = new Uri("https://api.telegram.org/"))
            .AddStandardResilienceHandler();
        services.AddTransient<IReportDeliveryChannel>(sp =>
            sp.GetRequiredService<TelegramDeliveryChannel>());
        services.AddTransient<IReportDeliveryChannel, EmailDeliveryChannel>();

        services.AddTransient<IDailyReportService, DailyReportService>();

        if (configuration.GetValue<bool>($"{DailyReportOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<DailyReportBackgroundService>();
        }

        AddAlertServices(services, configuration);

        return services;
    }

    /// <summary>
    /// Price alerts (opt-in via Alerts:Enabled). Re-evaluates saved analyses and notifies on
    /// invalidation/target through the same delivery channels as the daily report.
    /// </summary>
    private static void AddAlertServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AlertOptions>()
            .BindConfiguration(AlertOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IAlertService, AlertService>();

        if (configuration.GetValue<bool>($"{AlertOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<AlertBackgroundService>();
        }
    }
}
