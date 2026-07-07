using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Health;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="MarketDataHealthCheck"/>: healthy when the provider returns real candles for the
/// probe symbol; unhealthy when no provider supports it, when it returns nothing, or when it
/// throws (#173 AC1).
/// </summary>
[TestFixture]
public sealed class MarketDataHealthCheckTests
{
    private static readonly IReadOnlyList<MarketCandle> Candles =
        [new(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), 100m, 110m, 95m, 105m, 0m)];

    private static readonly HealthCheckContext Context = new()
    {
        Registration = new HealthCheckRegistration("market-data", _ => null!, null, null),
    };

    [Test]
    public async Task CheckHealthAsync_ProviderReturnsCandles_IsHealthy()
    {
        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports("BTC").Returns(true);
        provider.GetCandlesAsync("BTC", 5, Arg.Any<CancellationToken>()).Returns(Candles);
        var sut = new MarketDataHealthCheck([provider], NullLogger<MarketDataHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(Context);

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task CheckHealthAsync_NoProviderSupportsTheProbeSymbol_IsUnhealthy()
    {
        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports("BTC").Returns(false);
        var sut = new MarketDataHealthCheck([provider], NullLogger<MarketDataHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(Context);

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_ProviderReturnsNoCandles_IsUnhealthy()
    {
        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports("BTC").Returns(true);
        provider.GetCandlesAsync("BTC", 5, Arg.Any<CancellationToken>()).Returns([]);
        var sut = new MarketDataHealthCheck([provider], NullLogger<MarketDataHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(Context);

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_ProviderThrows_IsUnhealthy()
    {
        var provider = Substitute.For<IMarketDataProvider>();
        provider.Supports("BTC").Returns(true);
        provider.GetCandlesAsync("BTC", 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<MarketCandle>>(new HttpRequestException("boom")));
        var sut = new MarketDataHealthCheck([provider], NullLogger<MarketDataHealthCheck>.Instance);

        var result = await sut.CheckHealthAsync(Context);

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }
}
