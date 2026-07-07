using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for <c>POST /api/wave-analysis/sentiment</c>. No concrete
/// <c>ISentimentProvider</c> ships with this slice (#183), so the live stack always reports honest
/// "no coverage" — that is itself the behaviour under test (AC4): never a fabricated series, never a
/// 500, always an explicit reason; validation (symbol, annotations) and auth are also exercised over
/// the full stack.
/// </summary>
[TestFixture]
public sealed class SentimentAcceptanceTests
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

    private static object Request() => new
    {
        symbol = "BTC",
        annotations = new[]
        {
            new { date = "2024-01-05T00:00:00Z", price = 38_000m, label = "1" },
            new { date = "2024-01-15T00:00:00Z", price = 35_000m, label = "2" },
        },
    };

    [Test]
    public async Task Sentiment_NoProviderConfigured_ReturnsExplicitNoCoverage()
    {
        var response = await _client.PostAsJsonAsync("/api/wave-analysis/sentiment", Request());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("hasCoverage").GetBoolean(), Is.False);
            Assert.That(body.GetProperty("series").GetArrayLength(), Is.EqualTo(0));
            Assert.That(body.GetProperty("narrativeUnavailableReason").GetString(), Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task Sentiment_InvalidSymbol_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/wave-analysis/sentiment", new { symbol = "not a symbol!!", annotations = Array.Empty<object>() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Sentiment_NoAnnotations_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/wave-analysis/sentiment", new { symbol = "BTC", annotations = Array.Empty<object>() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Sentiment_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/wave-analysis/sentiment", Request());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
