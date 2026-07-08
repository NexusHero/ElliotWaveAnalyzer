using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the watchlist (<c>/api/watchlist</c>, #226): add → list → remove runs
/// through routing, auth, EF/PostgreSQL and serialization for real; default seeding and the
/// draft indicator are asserted against a dedicated fresh account each, so earlier tests in this
/// fixture cannot leave residue that changes their outcome.
/// </summary>
[TestFixture]
public sealed class WatchlistAcceptanceTests
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

    private async Task<HttpClient> NewUserClientAsync(string email)
    {
        var client = _factory.CreateClient();
        var credentials = new { email, password = "An0ther!Passw0rd", acceptTerms = true };
        await client.PostAsJsonAsync("/api/auth/register", credentials);
        var login = await client.PostAsJsonAsync("/api/auth/login", credentials);
        login.EnsureSuccessStatusCode();
        return client;
    }

    [Test]
    public async Task List_ForABrandNewUser_SeedsTheFourLegacyQuickSymbols()
    {
        using var client = await NewUserClientAsync("watchlist-fresh@example.com");

        var response = await client.GetAsync("/api/watchlist");
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var symbols = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("symbol").GetString()).ToList();
        Assert.That(symbols, Is.EqualTo(new[] { "SP500", "NASDAQ", "BTC", "ETH" }));
    }

    [Test]
    public async Task Add_ThenList_IncludesTheNewSymbol()
    {
        using var client = await NewUserClientAsync("watchlist-add@example.com");
        await client.GetAsync("/api/watchlist"); // trigger the default seed first

        var add = await client.PostAsJsonAsync("/api/watchlist", new { symbol = "AAPL" });
        Assert.That(add.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var list = await client.GetAsync("/api/watchlist");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var symbols = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("symbol").GetString()).ToList();

        Assert.That(symbols, Does.Contain("AAPL"));
    }

    [Test]
    public async Task Add_TheSameSymbolTwice_IsIdempotent()
    {
        using var client = await NewUserClientAsync("watchlist-dedupe@example.com");
        await client.PostAsJsonAsync("/api/watchlist", new { symbol = "AAPL" });
        await client.PostAsJsonAsync("/api/watchlist", new { symbol = "AAPL" });

        var list = await client.GetAsync("/api/watchlist");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var count = doc.RootElement.EnumerateArray().Count(e => e.GetProperty("symbol").GetString() == "AAPL");

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task Remove_DeletesTheSymbol()
    {
        using var client = await NewUserClientAsync("watchlist-remove@example.com");
        await client.PostAsJsonAsync("/api/watchlist", new { symbol = "AAPL" });

        var remove = await client.DeleteAsync("/api/watchlist/AAPL");
        Assert.That(remove.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var list = await client.GetAsync("/api/watchlist");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var symbols = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("symbol").GetString()).ToList();

        Assert.That(symbols, Does.Not.Contain("AAPL"));
    }

    [Test]
    public async Task Remove_UnknownSymbol_ReturnsNotFound()
    {
        using var client = await NewUserClientAsync("watchlist-remove-404@example.com");

        var remove = await client.DeleteAsync("/api/watchlist/NOSUCH");
        Assert.That(remove.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Entry_HasDraftIsTrueOnlyAfterADraftIsSaved()
    {
        using var client = await NewUserClientAsync("watchlist-hasdraft@example.com");

        var before = await client.GetAsync("/api/watchlist");
        var beforeBody = await before.Content.ReadAsStringAsync();
        using (var beforeDoc = JsonDocument.Parse(beforeBody))
        {
            var btc = beforeDoc.RootElement.EnumerateArray().First(e => e.GetProperty("symbol").GetString() == "BTC");
            Assert.That(btc.GetProperty("hasDraft").GetBoolean(), Is.False);
        }

        await client.PutAsJsonAsync("/api/workspace-drafts/BTC/1d", new
        {
            annotations = Array.Empty<object>(),
            settings = new
            {
                countType = "impulse",
                showInvalidationLayer = true,
                showSupportLayer = false,
                showTargetsLayer = false,
                showOscillator = false,
                logScale = false,
                subWaveDepth = (int?)null,
            },
        });

        var after = await client.GetAsync("/api/watchlist");
        var afterBody = await after.Content.ReadAsStringAsync();
        using var afterDoc = JsonDocument.Parse(afterBody);
        var btcAfter = afterDoc.RootElement.EnumerateArray().First(e => e.GetProperty("symbol").GetString() == "BTC");

        Assert.That(btcAfter.GetProperty("hasDraft").GetBoolean(), Is.True);
    }

    [Test]
    public async Task Add_InvalidSymbol_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/watchlist", new { symbol = "bad symbol!" });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task List_IsScopedToTheCallingUser()
    {
        using var userA = await NewUserClientAsync("watchlist-isolation-a@example.com");
        await userA.PostAsJsonAsync("/api/watchlist", new { symbol = "AAPL" });

        using var userB = await NewUserClientAsync("watchlist-isolation-b@example.com");
        var list = await userB.GetAsync("/api/watchlist");
        var body = await list.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var symbols = doc.RootElement.EnumerateArray().Select(e => e.GetProperty("symbol").GetString()).ToList();

        Assert.That(symbols, Does.Not.Contain("AAPL"),
            "a user must never see another user's watchlist additions");
    }

    [Test]
    public async Task Endpoints_RequireAuthentication()
    {
        using var anon = _factory.CreateClient();

        var list = await anon.GetAsync("/api/watchlist");
        Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
