using ElliotWaveAnalyzer.Api.Extensions;
using ElliotWaveAnalyzer.Api.Infrastructure.Reporting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// The scheduled portfolio-review refresh is opt-in: the hosted service is registered only when
/// <c>PortfolioReview:Enabled</c> is true (config-based registration, ADR-018 pattern).
/// </summary>
[TestFixture]
public sealed class PortfolioReviewScheduleRegistrationTests
{
    private static IServiceCollection Configure(bool enabled)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PortfolioReview:Enabled"] = enabled ? "true" : "false",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPortfolioReviewSchedule(config);
        return services;
    }

    [Test]
    public void AddPortfolioReviewSchedule_Disabled_DoesNotRegisterHostedService()
    {
        var hosted = Configure(enabled: false)
            .Where(d => d.ImplementationType == typeof(PortfolioRefreshBackgroundService));
        Assert.That(hosted, Is.Empty);
    }

    [Test]
    public void AddPortfolioReviewSchedule_Enabled_RegistersHostedService()
    {
        var hosted = Configure(enabled: true)
            .Where(d => d.ServiceType == typeof(IHostedService)
                && d.ImplementationType == typeof(PortfolioRefreshBackgroundService));
        Assert.That(hosted.Count(), Is.EqualTo(1));
    }
}
