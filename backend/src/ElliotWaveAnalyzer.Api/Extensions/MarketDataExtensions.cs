using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// Market data providers. Multiple IMarketDataProvider registrations are collected as
/// IEnumerable&lt;IMarketDataProvider&gt; by WaveAnalysisService + TechnicalAnalysisService.
/// Each concrete provider is wrapped in a caching decorator; selection happens at runtime
/// via Supports().
/// </summary>
internal static class MarketDataExtensions
{
    internal static IServiceCollection AddMarketDataProviders(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<CoinGeckoMarketDataProvider>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["MarketData:CoinGecko:BaseUrl"]
                ?? "https://api.coingecko.com/api/v3/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            var apiKey = configuration["MarketData:CoinGecko:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("x-cg-pro-api-key", apiKey);
            }
        })
        // Retry, timeout, and circuit-breaker for the rate-limited upstream API.
        .AddStandardResilienceHandler();

        // Yahoo Finance covers equity indices (NASDAQ, S&P 500). Yahoo rejects requests
        // without a User-Agent, so set one explicitly.
        services.AddHttpClient<YahooFinanceMarketDataProvider>(client =>
        {
            client.BaseAddress = new Uri(
                configuration["MarketData:Yahoo:BaseUrl"]
                ?? "https://query1.finance.yahoo.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "ElliotWaveAnalyzer/1.0");
        })
        .AddStandardResilienceHandler();

        // Each concrete provider is exposed as an IMarketDataProvider wrapped in a caching
        // decorator, so callers transparently get short-lived candle caching (Decorator/OCP).
        services.AddTransient<IMarketDataProvider>(sp =>
            new CachingMarketDataProvider(
                sp.GetRequiredService<CoinGeckoMarketDataProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingMarketDataProvider>>()));
        services.AddTransient<IMarketDataProvider>(sp =>
            new CachingMarketDataProvider(
                sp.GetRequiredService<YahooFinanceMarketDataProvider>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<CachingMarketDataProvider>>()));

        return services;
    }
}
