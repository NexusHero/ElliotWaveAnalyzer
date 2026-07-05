using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>Deterministic <see cref="IIntradayMarketDataProvider"/> for BTC/ETH with synthetic
/// hourly candles, so acceptance tests can exercise the 1H/4H path without network.</summary>
public sealed class FakeIntradayMarketDataProvider : IIntradayMarketDataProvider
{
    private static readonly HashSet<string> Supported =
        new(StringComparer.OrdinalIgnoreCase) { "BTC", "ETH" };

    public bool SupportsIntraday(string symbol) => Supported.Contains(symbol);

    public Task<IReadOnlyList<MarketCandle>> GetHourlyCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default)
    {
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // 200 hourly bars — enough for RSI/MACD to produce values past warm-up.
        IReadOnlyList<MarketCandle> candles =
        [
            .. Enumerable.Range(0, 200).Select(i =>
            {
                var price = 100m + (i % 20);
                return new MarketCandle(start.AddHours(i), price, price + 2m, price - 2m, price + 1m, 5m);
            }),
        ];
        return Task.FromResult(candles);
    }
}
