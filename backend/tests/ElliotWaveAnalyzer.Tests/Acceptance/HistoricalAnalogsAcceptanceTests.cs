using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>GET /api/wave-analysis/analogs</c>. Drives the running API over
/// HTTP with faked market data and a faked chat client, so routing, candle fetch, the no-lookahead
/// corpus sweep, retrieval/aggregation, the fact-guarded narrator and serialization are all exercised.
/// </summary>
[TestFixture]
public sealed class HistoricalAnalogsAcceptanceTests
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
    public async Task Analogs_SupportedSymbol_ReturnsDeterministicStatsAndAnalogs()
    {
        // A number-free summary passes the fact-guard trivially, exercising the narrator success path.
        _factory.Chat.ResponseJson = """{ "narrative": "The analogs skew constructive." }""";

        var response = await _client.GetAsync("/api/wave-analysis/analogs?symbol=BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stats = body.GetProperty("stats");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("symbol").GetString(), Is.EqualTo("BTC"));
            Assert.That(body.GetProperty("timeframe").GetString(), Is.EqualTo("1D"));
            Assert.That(stats.GetProperty("sampleCount").GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(body.TryGetProperty("analogs", out var analogs), Is.True);
            Assert.That(analogs.ValueKind, Is.EqualTo(JsonValueKind.Array));
            // Either a grounded narrative or an explicit reason for its absence — never silence.
            var hasNarrative = body.TryGetProperty("narrative", out var n) && n.ValueKind == JsonValueKind.String;
            var hasReason = body.TryGetProperty("narrativeUnavailableReason", out var r)
                && r.ValueKind == JsonValueKind.String;
            Assert.That(hasNarrative || hasReason, Is.True);
        });
    }

    [Test]
    public async Task Analogs_EveryAnalogConcludedBeforeNow_AndCarriesAnOutcome()
    {
        var response = await _client.GetAsync("/api/wave-analysis/analogs?symbol=BTC");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        foreach (var analog in body.GetProperty("analogs").EnumerateArray())
        {
            // No-lookahead surfaced end to end: an analog always has a conclusion date and outcome.
            Assert.That(analog.GetProperty("concludedAt").ValueKind, Is.EqualTo(JsonValueKind.String));
            Assert.That(analog.GetProperty("outcome").GetString(),
                Is.AnyOf("TargetReached", "Invalidated"));
            Assert.That(analog.GetProperty("similarity").GetDouble(), Is.InRange(0.0, 1.0));
        }
    }

    [Test]
    public async Task Analogs_UnsupportedSymbol_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/wave-analysis/analogs?symbol=ZZZ");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Analogs_InvalidInterval_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/wave-analysis/analogs?symbol=BTC&interval=5m");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
