namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Caching (per-instance). IMemoryCache backs candle caching; IDistributedMemoryCache
/// backs LLM response caching. Both are in-process here — for scaled-out instances, swap
/// the distributed cache for Redis (AddStackExchangeRedisCache) with no other changes.
/// </summary>
internal static class CachingExtensions
{
    internal static IServiceCollection AddAppCaching(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();

        return services;
    }
}
