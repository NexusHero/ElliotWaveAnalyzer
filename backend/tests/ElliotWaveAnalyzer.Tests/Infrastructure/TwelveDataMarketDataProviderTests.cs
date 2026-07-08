using System.Net;
using System.Text;
using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;
using Microsoft.Extensions.Logging.Abstractions;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="TwelveDataMarketDataProvider"/>. The HTTP boundary is stubbed with
/// canned Twelve Data <c>/time_series</c> JSON — no network. Twelve Data returns OHLCV fields as
/// JSON strings and rows newest-first; both quirks are exercised explicitly.
/// </summary>
[TestFixture]
public sealed class TwelveDataMarketDataProviderTests
{
    // Newest-first (as Twelve Data actually returns them). Row 2 has an unparseable open ("n/a")
    // and must be skipped; rows 1 and 3 are well-formed → 2 candles, ascending after sort.
    private const string TimeSeriesJson =
        """
        {
          "meta": { "symbol": "IXIC", "interval": "1day" },
          "values": [
            { "datetime": "2024-01-03", "open": "102.00", "high": "113.0", "low": "97.0", "close": "108.5", "volume": "3000" },
            { "datetime": "2024-01-02", "open": "n/a", "high": "112.0", "low": "96.0", "close": "107.0", "volume": "2000" },
            { "datetime": "2024-01-01", "open": "100.5", "high": "110.0", "low": "95.0", "close": "105.25", "volume": "1000" }
          ],
          "status": "ok"
        }
        """;

    private const string RateLimitErrorJson =
        """
        { "code": 429, "message": "You have run out of API credits for the current minute.", "status": "error" }
        """;

    private const string BadSymbolErrorJson =
        """
        { "code": 400, "message": "**symbol** not found", "status": "error" }
        """;

    private static (TwelveDataMarketDataProvider Sut, StubHttpMessageHandler Handler) Build(
        string json, string? apiKey = "test-key")
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.twelvedata.com/") };
        return (
            new TwelveDataMarketDataProvider(client, apiKey, NullLogger<TwelveDataMarketDataProvider>.Instance),
            handler);
    }

    [Test]
    public void Supports_RequiresAnApiKey_ThenIsCatchAllForAnyNonEmptySymbol()
    {
        var (withKey, _) = Build(TimeSeriesJson, apiKey: "test-key");
        var (withoutKey, _) = Build(TimeSeriesJson, apiKey: null);

        Assert.Multiple(() =>
        {
            Assert.That(withKey.Supports("RKLB"), Is.True);
            Assert.That(withKey.Supports("NASDAQ"), Is.True);
            Assert.That(withKey.Supports(" "), Is.False);
            Assert.That(withKey.SupportsIntraday("BTC"), Is.True);

            // No configured key → honest "unavailable" degradation, never an unauthenticated call.
            Assert.That(withoutKey.Supports("RKLB"), Is.False);
            Assert.That(withoutKey.SupportsIntraday("RKLB"), Is.False);
        });
    }

    [Test]
    public async Task GetCandlesAsync_ParsesStringFields_SortsAscending_SkipsUnparseableRows()
    {
        var (sut, _) = Build(TimeSeriesJson);

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Count.EqualTo(2)); // malformed "n/a" row skipped
            Assert.That(candles[0].Open, Is.EqualTo(100.5m));
            Assert.That(candles[0].Close, Is.EqualTo(105.25m));
            Assert.That(candles[0].Volume, Is.EqualTo(1000m));
            Assert.That(candles[1].Open, Is.EqualTo(102.00m));
            Assert.That(candles[1].OpenTime, Is.GreaterThan(candles[0].OpenTime)); // ascending
        });
    }

    [Test]
    public async Task GetCandlesAsync_MapsFriendlyAliases_AndRequestsDailyInterval()
    {
        var (nasdaq, nasdaqHandler) = Build(TimeSeriesJson);
        await nasdaq.GetCandlesAsync("NASDAQ", 30);

        var (btc, btcHandler) = Build(TimeSeriesJson);
        await btc.GetCandlesAsync("BTC", 30);

        Assert.Multiple(() =>
        {
            Assert.That(nasdaqHandler.LastRequest!.RequestUri!.ToString(), Does.Contain("IXIC"));
            Assert.That(nasdaqHandler.LastRequest!.RequestUri!.ToString(), Does.Contain("interval=1day"));
            Assert.That(btcHandler.LastRequest!.RequestUri!.ToString(), Does.Contain("BTC%2FUSD"));
        });
    }

    [Test]
    public async Task GetCandlesAsync_PassesThroughUnmappedTicker()
    {
        var (sut, handler) = Build(TimeSeriesJson);

        var candles = await sut.GetCandlesAsync("RKLB", 30);

        Assert.That(candles, Has.Count.EqualTo(2));
        Assert.That(handler.LastRequest!.RequestUri!.ToString(), Does.Contain("RKLB"));
    }

    [Test]
    public async Task GetHourlyCandlesAsync_RequestsOneHourInterval()
    {
        var (sut, handler) = Build(TimeSeriesJson);

        var candles = await sut.GetHourlyCandlesAsync("RKLB", 10);

        Assert.Multiple(() =>
        {
            Assert.That(candles, Has.Count.EqualTo(2));
            Assert.That(handler.LastRequest!.RequestUri!.ToString(), Does.Contain("interval=1h"));
        });
    }

    [Test]
    public void GetCandlesAsync_AvailabilityError_ThrowsHttpRequestException()
    {
        // Twelve Data returns HTTP 200 with a JSON error body for rate-limit/auth failures — this
        // must still surface as HttpRequestException so it reaches MarketDataEndpoints' existing
        // "provider unavailable, try again later" 502 path (#170 AC2), not a silently empty chart.
        var (sut, _) = Build(RateLimitErrorJson);

        Assert.ThrowsAsync<HttpRequestException>(() => sut.GetCandlesAsync("RKLB", 30));
    }

    [Test]
    public async Task GetCandlesAsync_RequestLevelError_ReturnsEmpty_WithoutThrowing()
    {
        // An unrecognized symbol (code 400) is a request problem, not a provider-availability
        // problem — honest "no data" rather than a misleading "provider unavailable" 502.
        var (sut, _) = Build(BadSymbolErrorJson);

        var candles = await sut.GetCandlesAsync("NOTASYMBOL", 30);

        Assert.That(candles, Is.Empty);
    }

    [Test]
    public async Task GetCandlesAsync_EmptyValues_ReturnsEmpty()
    {
        var (sut, _) = Build("""{ "values": [], "status": "ok" }""");

        var candles = await sut.GetCandlesAsync("NASDAQ", 30);

        Assert.That(candles, Is.Empty);
    }

    [Test]
    public void GetCandlesAsync_NonSuccessStatus_Throws()
    {
        var handler = new StubHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.twelvedata.com/") };
        var sut = new TwelveDataMarketDataProvider(client, "test-key", NullLogger<TwelveDataMarketDataProvider>.Instance);

        Assert.ThrowsAsync<HttpRequestException>(() => sut.GetCandlesAsync("BTC", 30));
    }
}
