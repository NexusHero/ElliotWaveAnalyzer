using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="YahooFinanceMarketDataProvider"/>. The HTTP boundary is
/// stubbed with canned Yahoo chart JSON — no network involved.
/// </summary>
[TestFixture]
public sealed class YahooFinanceMarketDataProviderTests
{
    // Row 1 is complete, row 2 has a null open (a trading gap → must be skipped),
    // row 3 is complete. Expect 2 candles, ascending by date.
    private const string ChartJson =
        """
        {
          "chart": {
            "result": [
              {
                "timestamp": [1704067200, 1704153600, 1704240000],
                "indicators": {
                  "quote": [
                    {
                      "open":   [100.5, null,  102.0],
                      "high":   [110.0, 112.0, 113.0],
                      "low":    [95.0,  96.0,  97.0],
                      "close":  [105.25, 107.0, 108.5],
                      "volume": [1000, 2000, 3000]
                    }
                  ]
                }
              }
            ],
            "error": null
          }
        }
        """;

    private static YahooFinanceMarketDataProvider Build(string json) =>
        new(
            StubHttpMessageHandler.JsonClient(json, "https://query1.finance.yahoo.com/"),
            NullLogger<YahooFinanceMarketDataProvider>.Instance);

    [Test]
    public void Supports_KnownIndices_ReturnsTrue()
    {
        var sut = Build(ChartJson);

        Assert.That(sut.Supports("NASDAQ"), Is.True);
        Assert.That(sut.Supports("SP500"), Is.True);
        Assert.That(sut.Supports("nasdaq"), Is.True); // case-insensitive
    }

    [Test]
    public void Supports_UnknownSymbol_ReturnsFalse() => Assert.That(Build(ChartJson).Supports("BTC"), Is.False);

    [Test]
    public async Task GetCandlesAsync_ParsesOhlcAndSkipsNullRows()
    {
        var sut = Build(ChartJson);

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.That(candles, Has.Count.EqualTo(2));
        Assert.That(candles[0].Open, Is.EqualTo(100.5m));
        Assert.That(candles[0].Close, Is.EqualTo(105.25m));
        Assert.That(candles[0].Volume, Is.EqualTo(1000m));
        Assert.That(candles[1].Open, Is.EqualTo(102.0m));
        // Ascending by date
        Assert.That(candles[1].OpenTime, Is.GreaterThan(candles[0].OpenTime));
    }

    [Test]
    public async Task GetCandlesAsync_EmptyResult_ReturnsEmpty()
    {
        var sut = Build("""{ "chart": { "result": [], "error": null } }""");

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.That(candles, Is.Empty);
    }

    [Test]
    public void GetCandlesAsync_UnsupportedSymbol_Throws()
    {
        var sut = Build(ChartJson);

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => sut.GetCandlesAsync("BTC", 30));
    }
}
