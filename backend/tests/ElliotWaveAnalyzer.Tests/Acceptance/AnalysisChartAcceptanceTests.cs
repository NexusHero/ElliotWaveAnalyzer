using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the annotated-chart export (<c>GET /api/analyses/{id}/chart.png</c>):
/// a saved synthetic analysis renders to a non-trivial <c>image/png</c> over the full stack (auth,
/// EF/PostgreSQL, the faked market-data candles, the composer and the SkiaSharp backend); ownership
/// and authentication are enforced. Only the LLM and market-data boundaries are faked.
/// </summary>
[TestFixture]
public sealed class AnalysisChartAcceptanceTests
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

    private static object BuildRequest(string symbol = "BTC") => new
    {
        symbol,
        structure = "Impulse",
        bullish = true,
        invalidationPrice = 30_000m,
        invalidationAbove = false,
        targetLow = 60_000m,
        targetHigh = 65_000m,
        entryLow = 38_000m,
        entryHigh = 40_000m,
        confidence = "high",
        score = 0.82m,
    };

    private async Task<Guid> SaveAsync(string symbol = "BTC")
    {
        var save = await _client.PostAsJsonAsync("/api/analyses", BuildRequest(symbol));
        var created = await save.Content.ReadFromJsonAsync<JsonElement>();
        return created.GetProperty("id").GetGuid();
    }

    [Test]
    public async Task GetChart_ForSavedAnalysis_ReturnsNonTrivialPng()
    {
        var id = await SaveAsync();

        var response = await _client.GetAsync($"/api/analyses/{id}/chart.png");
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
            Assert.That(bytes.Length, Is.GreaterThan(10_000), "a publication-grade chart is well over 10KB");
            // PNG magic number.
            Assert.That(bytes[..4], Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        });
    }

    [Test]
    public async Task GetChart_OtherUsersAnalysis_Returns404()
    {
        var id = await SaveAsync();

        using var otherClient = _factory.CreateClient();
        var credentials = new { email = "chart-other@example.com", password = "An0ther!Passw0rd" };
        await otherClient.PostAsJsonAsync("/api/auth/register", credentials);
        await otherClient.PostAsJsonAsync("/api/auth/login", credentials);

        var response = await otherClient.GetAsync($"/api/analyses/{id}/chart.png");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetChart_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/analyses/{Guid.NewGuid()}/chart.png");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetChart_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync($"/api/analyses/{Guid.NewGuid()}/chart.png");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
