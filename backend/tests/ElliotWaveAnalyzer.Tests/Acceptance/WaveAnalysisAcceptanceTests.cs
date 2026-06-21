using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>POST /api/wave-analysis</c> and <c>GET /api/tokens</c>.
/// Only the LLM is faked; the request flows through routing, model binding, input
/// validation, candle fetching, prompt building, response parsing, token tracking, and
/// serialization for real.
/// </summary>
[TestFixture]
public sealed class WaveAnalysisAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new AcceptanceWebApplicationFactory();
        _client = _factory.CreateClient();
        await _factory.AuthenticateAsync(_client);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object BuildValidRequest() => new
    {
        symbol = "BTC",
        annotations = new[]
        {
            new { date = "2024-01-05T00:00:00Z", price = 38_000m, label = "1" },
            new { date = "2024-01-15T00:00:00Z", price = 35_000m, label = "2" },
            new { date = "2024-02-01T00:00:00Z", price = 52_000m, label = "3" },
        },
    };

    [Test]
    public async Task PostWaveAnalysis_ValidRequest_Returns200WithResultAndUsage()
    {
        var response = await _client.PostAsJsonAsync("/api/wave-analysis", BuildValidRequest());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var result = body.GetProperty("result");
        Assert.That(result.GetProperty("isValid").GetBoolean(), Is.True);
        Assert.That(result.GetProperty("confidence").GetString(), Is.EqualTo("high"));
        Assert.That(result.GetProperty("warnings").GetArrayLength(), Is.EqualTo(1));

        var usage = body.GetProperty("usage");
        Assert.That(usage.GetProperty("provider").GetString(), Is.EqualTo("Gemini"));
        Assert.That(usage.GetProperty("totalTokens").GetInt32(), Is.EqualTo(150));
    }

    [Test]
    public async Task PostWaveAnalysis_TooFewAnnotations_Returns400()
    {
        var request = new
        {
            symbol = "BTC",
            annotations = new[]
            {
                new { date = "2024-01-05T00:00:00Z", price = 38_000m, label = "1" },
            },
        };

        var response = await _client.PostAsJsonAsync("/api/wave-analysis", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task PostWaveAnalysis_InvalidLabel_Returns400()
    {
        var request = new
        {
            symbol = "BTC",
            annotations = new[]
            {
                new { date = "2024-01-05T00:00:00Z", price = 38_000m, label = "1" },
                new { date = "2024-01-15T00:00:00Z", price = 35_000m, label = "NOPE" },
            },
        };

        var response = await _client.PostAsJsonAsync("/api/wave-analysis", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TokensEndpoint_AfterValidation_ReflectsRecordedUsage()
    {
        // A fresh factory so the singleton token tracker starts empty for this assertion.
        using var factory = new AcceptanceWebApplicationFactory();
        using var client = factory.CreateClient();
        await factory.AuthenticateAsync(client);

        await client.PostAsJsonAsync("/api/wave-analysis", BuildValidRequest());

        var response = await client.GetAsync("/api/tokens");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("sessionCallCount").GetInt32(), Is.EqualTo(1));
        Assert.That(body.GetProperty("sessionTotalTokens").GetInt32(), Is.EqualTo(150));
        Assert.That(body.GetProperty("tokensByProvider").GetProperty("Gemini").GetInt32(), Is.EqualTo(150));
    }
}
