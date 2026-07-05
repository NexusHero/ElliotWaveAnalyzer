using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Wires the optional scheduled portfolio-review refresh (opt-in via <c>PortfolioReview:Enabled</c>).
/// The on-demand review service is registered with the depot services; this only adds the cron
/// warm-through, mirroring the alert scheduler (ADR-018) — one config-gated hosted-service line.
/// </summary>
internal static class PortfolioReviewExtensions
{
    internal static IServiceCollection AddPortfolioReviewSchedule(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PortfolioReviewOptions>()
            .BindConfiguration(PortfolioReviewOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        if (configuration.GetValue<bool>($"{PortfolioReviewOptions.SectionName}:Enabled"))
        {
            services.AddHostedService<PortfolioRefreshBackgroundService>();
        }

        return services;
    }
}
