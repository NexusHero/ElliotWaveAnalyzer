using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>GET /api/market-data/{symbol}</c>.
/// Drives the running API over HTTP; only the market-data source is faked, so routing,
/// serialization, provider selection, and real RSI/MACD calculation are all exercised.
/// </summary>
[TestFixture]
public sealed class MarketDataAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient();
        await _factory.AuthenticateAsync(_client);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task GetMarketData_SupportedSymbol_Returns200WithCandlesAndIndicators()
    {
        var response = await _client.GetAsync("/api/market-data/BTC?days=90");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("symbol").GetString(), Is.EqualTo("BTC"));
        Assert.That(body.GetProperty("candles").GetArrayLength(), Is.GreaterThan(0));
        // RSI and MACD are computed for real by SkenderIndicatorCalculator.
        Assert.That(body.GetProperty("rsi").GetArrayLength(), Is.GreaterThan(0));
        Assert.That(body.GetProperty("macd").GetArrayLength(), Is.GreaterThan(0));
    }

    [Test]
    public async Task GetMarketData_UnsupportedSymbol_Returns400()
    {
        var response = await _client.GetAsync("/api/market-data/DOGE");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetMarketData_InvalidDays_Returns400()
    {
        var response = await _client.GetAsync("/api/market-data/BTC?days=0");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetMarketData_WeeklyInterval_ReturnsFewerCandlesThanDaily()
    {
        var daily = await _client.GetFromJsonAsync<JsonElement>("/api/market-data/BTC?days=90&interval=1d");
        var weekly = await _client.GetFromJsonAsync<JsonElement>("/api/market-data/BTC?days=90&interval=1w");

        var dailyCount = daily.GetProperty("candles").GetArrayLength();
        var weeklyCount = weekly.GetProperty("candles").GetArrayLength();
        Assert.Multiple(() =>
        {
            Assert.That(weeklyCount, Is.GreaterThan(0));
            Assert.That(weeklyCount, Is.LessThan(dailyCount), "weekly aggregation yields fewer bars");
            Assert.That(weeklyCount, Is.InRange(12, 15)); // ~90 days ≈ 13 ISO weeks
        });
    }

    [Test]
    public async Task GetMarketData_UnknownInterval_Returns400()
    {
        var response = await _client.GetAsync("/api/market-data/BTC?interval=5m");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task GetMarketData_FourHourInterval_Returns200WithCandlesAndIndicators()
    {
        var response = await _client.GetAsync("/api/market-data/BTC?days=30&interval=4h");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("candles").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(body.GetProperty("rsi").GetArrayLength(), Is.GreaterThan(0));
            Assert.That(body.GetProperty("macd").GetArrayLength(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task GetMarketData_MalformedSymbol_Returns400()
    {
        var response = await _client.GetAsync("/api/market-data/bad$sym");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
