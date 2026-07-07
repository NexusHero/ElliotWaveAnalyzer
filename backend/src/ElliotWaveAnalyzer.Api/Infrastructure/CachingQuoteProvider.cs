using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Caching decorator over an <see cref="IQuoteProvider"/> (Decorator/OCP — the wrapped provider is
/// unaware of caching), so re-opening a depot review doesn't re-hit the upstream market-data source
/// for every position on every request (#114, rate-limiting requirement). A short window — prices
/// move, unlike the symbol metadata <see cref="CachingSymbolResolver"/> caches for hours.
/// </summary>
internal sealed class CachingQuoteProvider(
    IQuoteProvider inner,
    IMemoryCache cache,
    ILogger<CachingQuoteProvider> logger) : IQuoteProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public async Task<decimal?> GetLatestPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var key = $"quote:{symbol.Trim().ToUpperInvariant()}";

        return await cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            logger.LogDebug("Cache miss for {Key}; resolving via {Provider}", key, inner.GetType().Name);
            return inner.GetLatestPriceAsync(symbol, cancellationToken);
        });
    }
}
