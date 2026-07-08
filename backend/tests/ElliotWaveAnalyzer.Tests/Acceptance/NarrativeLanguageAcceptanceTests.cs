using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the narrative-language preference (<c>/api/settings/narrative-language</c>,
/// #228): defaults to null (never chosen), the save round-trips, and it is scoped per user.
/// </summary>
[TestFixture]
public sealed class NarrativeLanguageAcceptanceTests
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
    public async Task Get_ForABrandNewUser_ReturnsNullLanguage()
    {
        using var client = await NewUserClientAsync("narrative-lang-fresh@example.com");

        var response = await client.GetAsync("/api/settings/narrative-language");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body.GetProperty("language").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [Test]
    public async Task Put_ThenGet_RoundTrips()
    {
        using var client = await NewUserClientAsync("narrative-lang-roundtrip@example.com");

        var put = await client.PutAsJsonAsync("/api/settings/narrative-language", new { language = "German" });
        Assert.That(put.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var get = await client.GetAsync("/api/settings/narrative-language");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.GetProperty("language").GetString(), Is.EqualTo("German"));
    }

    [Test]
    public async Task Put_IsScopedToTheCallingUser()
    {
        using var owner = await NewUserClientAsync("narrative-lang-owner@example.com");
        await owner.PutAsJsonAsync("/api/settings/narrative-language", new { language = "German" });

        using var other = await NewUserClientAsync("narrative-lang-other@example.com");
        var get = await other.GetAsync("/api/settings/narrative-language");
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();

        Assert.That(body.GetProperty("language").ValueKind, Is.EqualTo(JsonValueKind.Null),
            "a user must never see another user's narrative-language preference");
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/settings/narrative-language");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private async Task<HttpClient> NewUserClientAsync(string email)
    {
        var client = _factory.CreateClient();
        var credentials = new { email, password = "An0ther!Passw0rd", acceptTerms = true };
        await client.PostAsJsonAsync("/api/auth/register", credentials);
        var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
        login.EnsureSuccessStatusCode();
        return client;
    }
}
