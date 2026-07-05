using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Caching decorator over an <see cref="IIntradayMarketDataProvider"/> (Decorator/OCP), mirroring
/// <see cref="CachingMarketDataProvider"/> for the hourly path. Intraday endpoints are the most
/// rate-limited, so caching identical (symbol, days) hourly requests for a short window is what
/// keeps repeated 1H/4H analysis from throttling upstream.
/// </summary>
internal sealed class CachingIntradayMarketDataProvider(
    IIntradayMarketDataProvider inner,
    IMemoryCache cache,
    ILogger<CachingIntradayMarketDataProvider> logger) : IIntradayMarketDataProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public bool SupportsIntraday(string symbol) => inner.SupportsIntraday(symbol);

    public async Task<IReadOnlyList<MarketCandle>> GetHourlyCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
    {
        var key = $"hourly:{inner.GetType().Name}:{symbol.ToUpperInvariant()}:{days}";

        var candles = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            logger.LogDebug("Cache miss for {Key}; fetching from {Provider}", key, inner.GetType().Name);
            return inner.GetHourlyCandlesAsync(symbol, days, cancellationToken);
        });

        return candles ?? [];
    }
}
