using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// Boots the real API in-memory (full DI graph, routing, serialization, middleware,
/// endpoint and service logic) and only fakes the two external boundaries — the LLM
/// (<see cref="IChatClient"/>) and the market-data source (<see cref="IMarketDataProvider"/>).
///
/// Everything between the HTTP request and those boundaries is exercised for real, so
/// these are genuine end-to-end acceptance tests that need no network, secrets, or
/// live services and run deterministically in CI.
/// </summary>
public sealed class AcceptanceWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>The faked LLM. Tests can tweak its canned response before calling the API.</summary>
    public FakeChatClient Chat { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            // Swap the LLM for a deterministic fake.
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(Chat);

            // Swap the (caching-decorated CoinGecko) market data for a deterministic fake.
            services.RemoveAll<IMarketDataProvider>();
            services.AddSingleton<IMarketDataProvider, FakeMarketDataProvider>();
        });
    }
}

/// <summary>Deterministic <see cref="IChatClient"/> returning a configurable canned response.</summary>
public sealed class FakeChatClient : IChatClient
{
    /// <summary>Raw assistant text the analyzer will parse. Defaults to a valid wave assessment.</summary>
    public string ResponseJson { get; set; } =
        """
        { "isValid": true, "violations": [], "warnings": ["Wave 2 retracement is shallow"],
          "analysis": "A clean five-wave impulse.", "confidence": "high" }
        """;

    public UsageDetails Usage { get; set; } = new()
    {
        InputTokenCount = 100,
        OutputTokenCount = 50,
        TotalTokenCount = 150,
    };

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseJson))
        {
            Usage = Usage,
        });

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming is not used by the wave analyzer.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}

/// <summary>Deterministic <see cref="IMarketDataProvider"/> for BTC/ETH with synthetic candles.</summary>
public sealed class FakeMarketDataProvider : IMarketDataProvider
{
    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase) { "BTC", "ETH" };

    public bool Supports(string symbol) => Supported.Contains(symbol);

    public Task<IReadOnlyList<MarketCandle>> GetCandlesAsync(
        string symbol,
        int days,
        CancellationToken cancellationToken = default)
        => Task.FromResult(MarketDataFixtures.CreateCandles(days));
}
