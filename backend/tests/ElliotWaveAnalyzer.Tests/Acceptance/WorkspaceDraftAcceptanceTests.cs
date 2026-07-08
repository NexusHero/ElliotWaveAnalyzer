using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for per-symbol workspace drafts (<c>/api/workspace-drafts</c>, #226): the
/// save → get → restore round-trip runs through routing, auth, EF/PostgreSQL and serialization for
/// real. Per-user isolation ("another device") is asserted with a second account.
/// </summary>
[TestFixture]
public sealed class WorkspaceDraftAcceptanceTests
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

    private static object BuildDraft() => new
    {
        annotations = new[]
        {
            new { date = "2024-01-01T00:00:00Z", price = 100m, label = "1" },
            new { date = "2024-01-05T00:00:00Z", price = 130m, label = "2" },
        },
        settings = new
        {
            countType = "impulse",
            showInvalidationLayer = true,
            showSupportLayer = false,
            showTargetsLayer = false,
            showOscillator = true,
            logScale = false,
            subWaveDepth = (int?)null,
        },
    };

    [Test]
    public async Task Save_ThenGet_RestoresTheDraftExactly()
    {
        var save = await _client.PutAsJsonAsync("/api/workspace-drafts/AAPL/1d", BuildDraft());
        Assert.That(save.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await _client.GetAsync("/api/workspace-drafts/AAPL/1d");
        var body = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var annotations = root.GetProperty("annotations").EnumerateArray().ToList();
        Assert.Multiple(() =>
        {
            Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.GetProperty("symbol").GetString(), Is.EqualTo("AAPL"));
            Assert.That(root.GetProperty("interval").GetString(), Is.EqualTo("1d"));
            Assert.That(annotations, Has.Count.EqualTo(2));
            Assert.That(annotations[1].GetProperty("label").GetString(), Is.EqualTo("2"));
            Assert.That(root.GetProperty("settings").GetProperty("countType").GetString(), Is.EqualTo("impulse"));
            Assert.That(root.GetProperty("settings").GetProperty("showOscillator").GetBoolean(), Is.True);
        });
    }

    [Test]
    public async Task Save_Twice_OverwritesInPlace()
    {
        await _client.PutAsJsonAsync("/api/workspace-drafts/MSFT/1d", BuildDraft());

        var second = new
        {
            annotations = new[] { new { date = "2024-02-01T00:00:00Z", price = 50m, label = "A" } },
            settings = new
            {
                countType = "zigzag",
                showInvalidationLayer = true,
                showSupportLayer = false,
                showTargetsLayer = false,
                showOscillator = false,
                logScale = true,
                subWaveDepth = (int?)null,
            },
        };
        await _client.PutAsJsonAsync("/api/workspace-drafts/MSFT/1d", second);

        var get = await _client.GetAsync("/api/workspace-drafts/MSFT/1d");
        var body = await get.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.That(root.GetProperty("annotations").GetArrayLength(), Is.EqualTo(1));
        Assert.That(root.GetProperty("settings").GetProperty("countType").GetString(), Is.EqualTo("zigzag"));
    }

    [Test]
    public async Task Get_NoDraftForSymbol_ReturnsNotFound()
    {
        var get = await _client.GetAsync("/api/workspace-drafts/NOSUCH/1d");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_RemovesTheDraft()
    {
        await _client.PutAsJsonAsync("/api/workspace-drafts/TSLA/1d", BuildDraft());

        var delete = await _client.DeleteAsync("/api/workspace-drafts/TSLA/1d");
        Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await _client.GetAsync("/api/workspace-drafts/TSLA/1d");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var again = await _client.DeleteAsync("/api/workspace-drafts/TSLA/1d");
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Draft_SurvivesAcrossASecondClient_ScopedToTheSameUser()
    {
        // Simulates "another device": a fresh HttpClient (no shared cookie jar) logging back into
        // the SAME account should see the draft (AC2 — server-side, per-user, not per-session).
        await _client.PutAsJsonAsync("/api/workspace-drafts/NVDA/1d", BuildDraft());

        using var otherDeviceClient = _factory.CreateClient();
        await _factory.AuthenticateAsync(otherDeviceClient);

        var get = await otherDeviceClient.GetAsync("/api/workspace-drafts/NVDA/1d");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Draft_IsScopedToTheCallingUser()
    {
        await _client.PutAsJsonAsync("/api/workspace-drafts/GOOG/1d", BuildDraft());

        using var otherUserClient = _factory.CreateClient();
        var credentials = new { email = "draft-other@example.com", password = "An0ther!Passw0rd", acceptTerms = true };
        await otherUserClient.PostAsJsonAsync("/api/auth/register", credentials);
        var login = await otherUserClient.PostAsJsonAsync("/api/auth/login", credentials);
        login.EnsureSuccessStatusCode();

        var get = await otherUserClient.GetAsync("/api/workspace-drafts/GOOG/1d");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
            "a user must never see another user's workspace draft");
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();

        var get = await anon.GetAsync("/api/workspace-drafts/BTC/1d");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Save_InvalidInterval_ReturnsBadRequest()
    {
        var response = await _client.PutAsJsonAsync("/api/workspace-drafts/BTC/3m", BuildDraft());
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
