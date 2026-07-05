using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

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
