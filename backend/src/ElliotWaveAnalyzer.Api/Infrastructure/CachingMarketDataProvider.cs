using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Caching decorator over an <see cref="IMarketDataProvider"/>. Adds short-lived
/// in-memory caching of candle responses without the wrapped provider knowing about it
/// (Decorator pattern — Open/Closed: caching is added by composition, not by editing
/// CoinGecko/Yahoo/etc.).
///
/// WHY this matters for scalability:
/// upstream market APIs are rate-limited (CoinGecko free tier: ~10–30 req/min). Under
/// concurrent load or repeated requests for the same symbol, hitting the network every
/// time throttles and slows the app. Caching identical (symbol, days) requests for a
/// short window absorbs that. <see cref="IMemoryCache"/> is per-instance; swap the
/// registration for an <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// based decorator to share the cache across scaled-out instances.
/// </summary>
public sealed class CachingMarketDataProvider(
    IMarketDataProvider inner,
    IMemoryCache cache,
    ILogger<CachingMarketDataProvider> logger) : IMarketDataProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    /// <inheritdoc/>
    public bool Supports(string symbol) => inner.Supports(symbol);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
    {
        var key = $"candles:{inner.GetType().Name}:{symbol.ToUpperInvariant()}:{days}";

        var candles = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            logger.LogDebug("Cache miss for {Key}; fetching from {Provider}", key, inner.GetType().Name);
            return inner.GetCandlesAsync(symbol, days, cancellationToken);
        });

        return candles ?? [];
    }
}
