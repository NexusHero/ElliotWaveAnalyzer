using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Market data providers. Multiple IMarketDataProvider registrations are collected as
/// IEnumerable&lt;IMarketDataProvider&gt; by WaveAnalysisService + TechnicalAnalysisService.
/// Each concrete provider is wrapped in a caching decorator; selection happens at runtime
/// via Supports().
///
/// A single Twelve Data provider covers both OHLCV paths (daily + hourly, equities/indices/crypto)
/// that used to be split across the keyless Yahoo Finance and CoinGecko providers (#170, ADR-077).
/// Symbol *search* (ticker/name/ISIN → instrument) still goes through Yahoo's search endpoint — a
/// separate concern from OHLCV data, unaffected by this change.
/// </summary>
internal static class MarketDataExtensions
{
    internal static IServiceCollection AddMarketDataProviders(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Named (not typed-client) registration: the constructor also takes the configured API key,
        // which DI cannot resolve on its own, so the instance is built explicitly below.
        services.AddHttpClient(nameof(TwelveDataMarketDataProvider), client =>
        {
            client.BaseAddress = new Uri(
                configuration["MarketData:TwelveData:BaseUrl"] ?? "https://api.twelvedata.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        // Retry, timeout, and circuit-breaker for the rate-limited upstream API.
        .AddStandardResilienceHandler();

        services.AddTransient(sp =>
            new TwelveDataMarketDataProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(TwelveDataMarketDataProvider)),
                configuration["MarketData:TwelveData:ApiKey"],
                sp.GetRequiredService<ILogger<TwelveDataMarketDataProvider>>()));

        // Symbol resolution (ticker / name / ISIN → instrument) via Yahoo's search endpoint. Yahoo
        // rejects requests without a User-Agent, so set one explicitly. Cached aggressively since
        // instrument metadata is effectively static.
        var yahooBaseUrl = configuration["MarketData:Yahoo:BaseUrl"] ?? "https://query1.finance.yahoo.com/";
        services.AddHttpClient<YahooSymbolResolver>(client =>
        {
            client.BaseAddress = new Uri(yahooBaseUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "ElliotWaveAnalyzer/1.0");
        })
        .AddStandardResilienceHandler();

        services.AddTransient<ISymbolResolver>(sp =>
            new CachingSymbolResolver(
                sp.GetRequiredService<YahooSymbolResolver>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingSymbolResolver>>()));

        // Twelve Data is also the intraday (hourly) source, exposed via IIntradayMarketDataProvider
        // and wrapped in the same short-lived caching decorator as the daily path.
        services.AddTransient<IIntradayMarketDataProvider>(sp =>
            new CachingIntradayMarketDataProvider(
                sp.GetRequiredService<TwelveDataMarketDataProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingIntradayMarketDataProvider>>()));

        // Exposed as an IMarketDataProvider wrapped in a caching decorator, so callers
        // transparently get short-lived candle caching (Decorator/OCP).
        services.AddTransient<IMarketDataProvider>(sp =>
            new CachingMarketDataProvider(
                sp.GetRequiredService<TwelveDataMarketDataProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingMarketDataProvider>>()));

        return services;
    }
}
