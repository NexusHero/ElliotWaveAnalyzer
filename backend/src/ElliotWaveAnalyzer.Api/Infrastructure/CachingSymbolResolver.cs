using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Caching decorator over an <see cref="ISymbolResolver"/> (Decorator/OCP — the wrapped resolver is
/// unaware of caching). Symbol metadata is effectively static, so resolutions are cached for a long
/// window, absorbing repeated lookups (e.g. re-analyzing the same depot) without hammering the
/// upstream search API.
/// </summary>
internal sealed class CachingSymbolResolver(
    ISymbolResolver inner,
    IMemoryCache cache,
    ILogger<CachingSymbolResolver> logger) : ISymbolResolver
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    public async Task<IReadOnlyList<ResolvedSymbol>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        var key = $"symbol-search:{query.Trim().ToUpperInvariant()}";

        var resolved = await cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            logger.LogDebug("Cache miss for {Key}; resolving via {Resolver}", key, inner.GetType().Name);
            return inner.SearchAsync(query, cancellationToken);
        });

        return resolved ?? [];
    }
}
