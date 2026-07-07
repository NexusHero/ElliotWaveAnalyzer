using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Health;

/// <summary>
/// Readiness check for the market-data provider (#173 AC1): a real, cheap probe request — not a
/// registration check — against a well-known symbol, so this only reports healthy when the
/// upstream is actually reachable. Runs through the same caching decorator every other market-data
/// call does, so a healthy upstream costs at most one live request per cache TTL, not one per
/// readiness poll.
/// </summary>
internal sealed class MarketDataHealthCheck(
    IEnumerable<IMarketDataProvider> providers, ILogger<MarketDataHealthCheck> logger) : IHealthCheck
{
    private const string ProbeSymbol = "BTC";
    private const int ProbeDays = 5;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var provider = providers.FirstOrDefault(p => p.Supports(ProbeSymbol));
        if (provider is null)
        {
            return HealthCheckResult.Unhealthy("No market data provider supports the probe symbol.");
        }

        try
        {
            var candles = await provider.GetCandlesAsync(ProbeSymbol, ProbeDays, cancellationToken);
            return candles.Count > 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Market data provider returned no candles for the probe symbol.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Market data health check failed");
            return HealthCheckResult.Unhealthy("Market data provider is unreachable.", ex);
        }
    }
}
