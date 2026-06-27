using ElliotWaveAnalyzer.Api.Application;
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

        return services;
    }
}
