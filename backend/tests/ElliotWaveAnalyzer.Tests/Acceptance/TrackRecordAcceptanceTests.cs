using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for the track record (<c>/api/analyses</c>): the save → list →
/// delete round-trip runs through routing, auth, EF/PostgreSQL and serialization for real; only
/// the LLM and market-data boundaries are faked. Per-user isolation is asserted with a second
/// account. Outcome evaluation itself is covered exhaustively by the pure evaluator unit tests.
/// </summary>
[TestFixture]
public sealed class TrackRecordAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
        confidence = "high",
        score = 0.82m,
    };

    [Test]
    public async Task Save_ThenList_ReturnsTheSavedAnalysis()
    {
        var save = await _client.PostAsJsonAsync("/api/analyses", BuildRequest());
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var list = await _client.GetAsync("/api/analyses");
        var body = await list.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.EnumerateArray().First();
        Assert.Multiple(() =>
        {
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(first.GetProperty("symbol").GetString(), Is.EqualTo("BTC"));
            Assert.That(first.GetProperty("structure").GetString(), Is.EqualTo("Impulse"));
            Assert.That(first.GetProperty("invalidationPrice").GetDecimal(), Is.EqualTo(30_000m));
            // Fixture candles predate 'now', so nothing has formed since the save → Pending.
            Assert.That(first.GetProperty("outcome").GetString(), Is.EqualTo("Pending"));
        });
    }

    [Test]
    public async Task Delete_RemovesTheAnalysis()
    {
        var save = await _client.PostAsJsonAsync("/api/analyses", BuildRequest("ETH"));
        var created = await save.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var delete = await _client.DeleteAsync($"/api/analyses/{id}");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // A second delete of the same id now 404s.
        var again = await _client.DeleteAsync($"/api/analyses/{id}");
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task List_IsScopedToTheCallingUser()
    {
        // User A saves an analysis.
        await _client.PostAsJsonAsync("/api/analyses", BuildRequest());

        // A brand-new user (fresh cookie jar) must not see it.
        using var otherClient = _factory.CreateClient();
        var credentials = new { email = "other@example.com", password = "An0ther!Passw0rd" };
        await otherClient.PostAsJsonAsync("/api/auth/register", credentials);
        await otherClient.PostAsJsonAsync("/api/auth/login", credentials);

        var list = await otherClient.GetAsync("/api/analyses");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(0),
            "a user must never see another user's saved analyses");
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();

        var list = await anon.GetAsync("/api/analyses");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Save_MissingSymbol_ReturnsBadRequest()
    {
        var bad = new { structure = "Impulse", bullish = true, confidence = "low" };
        var response = await _client.PostAsJsonAsync("/api/analyses", bad);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
