using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for authentication: registration, login (session cookie),
/// the current-user probe, logout, and that protected endpoints reject anonymous access.
/// </summary>
[TestFixture]
public sealed class AuthAcceptanceTests
{
    private AcceptanceWebApplicationFactory _factory = null!;

    // Fresh factory (and in-memory DB) per test so account state never leaks between tests.
    [SetUp]
    public void SetUp() => _factory = new AcceptanceWebApplicationFactory();

    [TearDown]
    public void TearDown() => _factory.Dispose();

    private static object Credentials => new
    {
        email = AcceptanceWebApplicationFactory.TestEmail,
        password = AcceptanceWebApplicationFactory.TestPassword,
    };

    [Test]
    public async Task Register_Login_Me_RoundTrips()
    {
        using var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register", Credentials);
        Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var login = await client.PostAsJsonAsync("/api/auth/login", Credentials);
        Assert.That(login.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var me = await client.GetAsync("/api/auth/me");
        Assert.That(me.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(body.GetProperty("email").GetString(), Is.EqualTo(AcceptanceWebApplicationFactory.TestEmail));
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutAuth_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/market-data/BTC");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Me_WithoutAuth_Returns401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Login_WithWrongPassword_Returns401()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", Credentials);

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = AcceptanceWebApplicationFactory.TestEmail,
            password = "wrong-password-123",
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Logout_InvalidatesSession()
    {
        using var client = _factory.CreateClient();
        await _factory.AuthenticateAsync(client);

        // Authenticated before logout.
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        await client.PostAsync("/api/auth/logout", content: null);

        // Session revoked → no longer authenticated.
        Assert.That((await client.GetAsync("/api/auth/me")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
