using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="CoinGeckoMarketDataProvider"/>. The HTTP boundary is stubbed with
/// canned CoinGecko OHLC JSON (array of [ts_ms, open, high, low, close]) — no network.
/// </summary>
[TestFixture]
public sealed class CoinGeckoMarketDataProviderTests
{
    // Two well-formed rows (out of order) + one malformed (too few fields, must be skipped).
    private const string OhlcJson =
        """
        [
          [1704153600000, 105.0, 112.0, 100.0, 108.0],
          [1704067200000, 100.0, 110.0, 95.0, 105.0],
          [1704240000000, 108.0]
        ]
        """;

    private static CoinGeckoMarketDataProvider Build(string json) =>
        new(
            StubHttpMessageHandler.JsonClient(json, "https://api.coingecko.com/api/v3/"),
            NullLogger<CoinGeckoMarketDataProvider>.Instance);

    [Test]
    public void Supports_BtcAndEth_CaseInsensitive()
    {
        var sut = Build(OhlcJson);
        Assert.Multiple(() =>
        {
            Assert.That(sut.Supports("BTC"), Is.True);
            Assert.That(sut.Supports("eth"), Is.True);
            Assert.That(sut.Supports("NASDAQ"), Is.False);
        });
    }

    [Test]
    public async Task GetCandlesAsync_MapsRowsOrderedByTime_SkipsMalformed_AndZeroesVolume()
    {
        var candles = await Build(OhlcJson).GetCandlesAsync("BTC", days: 30);

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Count.EqualTo(2)); // malformed row skipped
            Assert.That(candles[0].Open, Is.EqualTo(100.0m)); // earlier timestamp first
            Assert.That(candles[1].Open, Is.EqualTo(105.0m));
            Assert.That(candles[0].High, Is.EqualTo(110.0m));
            Assert.That(candles[0].Low, Is.EqualTo(95.0m));
            Assert.That(candles[0].Close, Is.EqualTo(105.0m));
            Assert.That(candles.All(c => c.Volume == 0m), Is.True); // endpoint omits volume
        });
    }

    [Test]
    public async Task GetCandlesAsync_EmptyPayload_ReturnsEmpty()
    {
        var candles = await Build("[]").GetCandlesAsync("ETH", days: 30);

        Assert.That(candles, Is.Empty);
    }
}
