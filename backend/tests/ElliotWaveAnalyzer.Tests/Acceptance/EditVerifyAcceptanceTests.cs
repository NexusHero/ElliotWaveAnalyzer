using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the analyst-in-the-loop re-verification (<c>POST /api/wave-analysis/verify</c>):
/// an edited annotation set comes back with the deterministic read (snapped pivots, rules, levels) over
/// the full stack (auth, the faked market-data provider, the deterministic verifier — no LLM);
/// unauthenticated → 401.
/// </summary>
[TestFixture]
public sealed class EditVerifyAcceptanceTests
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

    private static object EditedCount() => new
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
    public async Task Verify_EditedCount_ReturnsDeterministicRead()
    {
        var response = await _client.PostAsJsonAsync("/api/wave-analysis/verify", EditedCount());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(body.TryGetProperty("structure", out _), Is.True);
            Assert.That(body.GetProperty("rules").GetProperty("rules").GetArrayLength(), Is.GreaterThan(0));
            // No LLM narrative field — this path is purely deterministic.
            Assert.That(body.TryGetProperty("analysis", out _), Is.False);
        });
    }

    [Test]
    public async Task Verify_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/wave-analysis/verify", EditedCount());

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
