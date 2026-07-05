using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the per-user LLM key (REQ-013): after a user saves a key, an LLM-using
/// endpoint still succeeds over the full stack (the vault + the user-aware client wiring are exercised
/// through DI; the LLM boundary itself is the deterministic fake). The no-key fallback is covered by
/// <see cref="WaveAnalysisAcceptanceTests"/>.
/// </summary>
[TestFixture]
public sealed class PerUserKeyAcceptanceTests
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

    private static object AnalysisRequest() => new
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
    public async Task SaveKey_ThenAnalyze_Succeeds()
    {
        var save = await _client.PutAsJsonAsync("/api/keys/gemini", new { key = "gemini-user-key-1234" });
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var response = await _client.PostAsJsonAsync("/api/wave-analysis", AnalysisRequest());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("result").GetProperty("isValid").GetBoolean(), Is.True);
        });
    }
}
