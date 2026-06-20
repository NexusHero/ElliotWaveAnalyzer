using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.TestData;

/// <summary>
/// Deterministic test data factory for market candles.
/// All methods use a fixed seed so test results are reproducible.
/// </summary>
public static class MarketDataFixtures
{
    private const int DefaultSeed = 42;

    /// <summary>
    /// Creates <paramref name="count"/> candles with random-but-reproducible price movements
    /// starting at $40,000 from 2024-01-01.
    /// </summary>
    public static IReadOnlyList<MarketCandle> CreateCandles(int count)
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var price = 40_000m;
        var rng = new Random(DefaultSeed);
        var candles = new List<MarketCandle>(count);

        for (var i = 0; i < count; i++)
        {
            var open = price;
            var change = (decimal)(rng.NextDouble() * 2_000 - 1_000);
            var close = Math.Max(1m, price + change);
            var high = Math.Max(open, close) + (decimal)(rng.NextDouble() * 500);
            var low = Math.Max(1m, Math.Min(open, close) - (decimal)(rng.NextDouble() * 500));
            price = close;

            candles.Add(new MarketCandle(
                OpenTime: startDate.AddDays(i),
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: (decimal)(rng.NextDouble() * 1_000)));
        }

        return candles;
    }

    /// <summary>
    /// Creates candles with a consistent directional trend.
    /// Uptrend: Close rises by <paramref name="stepSize"/> per candle.
    /// Downtrend: Close falls by <paramref name="stepSize"/> per candle.
    /// Useful for testing directional indicator properties (e.g. MACD > 0 in uptrend).
    /// </summary>
    public static IReadOnlyList<MarketCandle> CreateTrendingCandles(
        bool uptrend,
        int count,
        decimal startPrice = 40_000m,
        decimal stepSize = 500m)
    {
        var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var direction = uptrend ? stepSize : -stepSize;
        var candles = new List<MarketCandle>(count);
        var price = startPrice;

        for (var i = 0; i < count; i++)
        {
            var open = price;
            var close = Math.Max(1m, price + direction);
            var high = Math.Max(open, close) + 100m;
            var low = Math.Max(1m, Math.Min(open, close) - 100m);
            price = close;

            candles.Add(new MarketCandle(
                OpenTime: startDate.AddDays(i),
                Open: open,
                High: high,
                Low: low,
                Close: close,
                Volume: 1_000m));
        }

        return candles;
    }

    /// <summary>
    /// 30 candles with strictly rising closes (+1 per day).
    /// Mathematical property: with all gains and no losses, RSI approaches 100.
    /// After warm-up period (period+1 candles), RSI should be &gt; 99.
    /// </summary>
    public static IReadOnlyList<MarketCandle> CreateAllGainsCandles(
        int count = 30, decimal startPrice = 100m) =>
        CreateTrendingCandles(uptrend: true, count: count, startPrice: startPrice, stepSize: 1m);

    /// <summary>
    /// 30 candles with strictly falling closes (−1 per day).
    /// Mathematical property: with all losses and no gains, RSI approaches 0.
    /// After warm-up period, RSI should be &lt; 1.
    /// </summary>
    public static IReadOnlyList<MarketCandle> CreateAllLossesCandles(
        int count = 30, decimal startPrice = 100m) =>
        CreateTrendingCandles(uptrend: false, count: count, startPrice: startPrice, stepSize: 1m);
}
