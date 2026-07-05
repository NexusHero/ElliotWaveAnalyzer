using System.Net;
using System.Text;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="YahooFinanceMarketDataProvider"/>. The HTTP boundary is stubbed with
/// canned Yahoo chart JSON — no network. Yahoo is the fallback daily provider (catch-all) and the
/// hourly intraday source; friendly aliases map to Yahoo symbols, other tickers pass through.
/// </summary>
[TestFixture]
public sealed class YahooFinanceMarketDataProviderTests
{
    // Row 1 complete, row 2 has a null open (trading gap → skipped), row 3 complete → 2 candles.
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

    private static (YahooFinanceMarketDataProvider Sut, StubHttpMessageHandler Handler) Build(string json)
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://query1.finance.yahoo.com/") };
        return (new YahooFinanceMarketDataProvider(client, NullLogger<YahooFinanceMarketDataProvider>.Instance), handler);
    }

    [Test]
    public void Supports_IsCatchAll_ForAnyNonEmptySymbol()
    {
        var (sut, _) = Build(ChartJson);
        Assert.Multiple(() =>
        {
            Assert.That(sut.Supports("RKLB"), Is.True);      // arbitrary equity
            Assert.That(sut.Supports("NASDAQ"), Is.True);    // alias
            Assert.That(sut.Supports(" "), Is.False);        // empty/whitespace
            Assert.That(sut.SupportsIntraday("SI=F"), Is.True);
        });
    }

    [Test]
    public async Task GetCandlesAsync_ParsesOhlcAndSkipsNullRows()
    {
        var (sut, _) = Build(ChartJson);

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Count.EqualTo(2));
            Assert.That(candles[0].Open, Is.EqualTo(100.5m));
            Assert.That(candles[0].Close, Is.EqualTo(105.25m));
            Assert.That(candles[0].Volume, Is.EqualTo(1000m));
            Assert.That(candles[1].Open, Is.EqualTo(102.0m));
            Assert.That(candles[1].OpenTime, Is.GreaterThan(candles[0].OpenTime)); // ascending
        });
    }

    [Test]
    public async Task GetCandlesAsync_MapsFriendlyAlias_AndRequestsDailyInterval()
    {
        var (sut, handler) = Build(ChartJson);

        await sut.GetCandlesAsync("NASDAQ", 30);

        var url = handler.LastRequest!.RequestUri!.ToString();
        Assert.Multiple(() =>
        {
            Assert.That(url, Does.Contain("IXIC")); // NASDAQ → ^IXIC (escaping-agnostic)
            Assert.That(url, Does.Contain("interval=1d"));
        });
    }

    [Test]
    public async Task GetCandlesAsync_PassesThroughUnmappedTicker()
    {
        var (sut, handler) = Build(ChartJson);

        var candles = await sut.GetCandlesAsync("RKLB", 30); // not an alias — no throw

        Assert.That(candles, Has.Count.EqualTo(2));
        Assert.That(handler.LastRequest!.RequestUri!.ToString(), Does.Contain("RKLB"));
    }

    [Test]
    public async Task GetHourlyCandlesAsync_RequestsSixtyMinuteInterval()
    {
        var (sut, handler) = Build(ChartJson);

        var candles = await sut.GetHourlyCandlesAsync("RKLB", 30);

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Count.EqualTo(2));
            Assert.That(handler.LastRequest!.RequestUri!.ToString(), Does.Contain("interval=60m"));
        });
    }

    [Test]
    public void GetHourlyCandlesAsync_BeyondLookback_ThrowsMarketDataRangeException()
    {
        var (sut, handler) = Build(ChartJson);

        var ex = Assert.ThrowsAsync<MarketDataRangeException>(
            () => sut.GetHourlyCandlesAsync("RKLB", YahooFinanceMarketDataProvider.MaxHourlyLookbackDays + 1));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.MaxDays, Is.EqualTo(YahooFinanceMarketDataProvider.MaxHourlyLookbackDays));
            Assert.That(handler.LastRequest, Is.Null, "must fail before any network call");
        });
    }

    [Test]
    public async Task GetCandlesAsync_EmptyResult_ReturnsEmpty()
    {
        var (sut, _) = Build("""{ "chart": { "result": [], "error": null } }""");

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.That(candles, Is.Empty);
    }
}
