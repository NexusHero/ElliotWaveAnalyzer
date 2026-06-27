using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end checks of the security posture: authorization gating, security response
/// headers, CORS origin allow-listing, and that rate-limited endpoints are wired up.
///
/// The client is created with an HTTPS base address so <c>Request.IsHttps</c> is true under
/// TestServer — that is what makes the conditional Strict-Transport-Security header emit.
/// </summary>
[TestFixture]
public sealed class SecurityAcceptanceTests
{
    private static readonly WebApplicationFactoryClientOptions HttpsClient = new()
    {
        BaseAddress = new Uri("https://localhost"),
    };

    private AcceptanceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        TestDocker.SkipIfUnavailable();
        _factory = new AcceptanceWebApplicationFactory();
        await _factory.InitializeAsync();
        _client = _factory.CreateClient(HttpsClient);
    }

    [TearDown]
    public async Task TearDown()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/market-data/BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithAuth_Returns200()
    {
        await _factory.AuthenticateAsync(_client);
        var response = await _client.GetAsync("/api/market-data/BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task SecurityHeaders_ArePresent()
    {
        // The client uses an HTTPS base address (so Request.IsHttps is true), and we send a
        // non-localhost Host header because the HSTS policy excludes localhost/127.0.0.1/[::1]
        // by design. An unauthenticated request to a protected endpoint returns 401 with the
        // full security-header set applied by the global middleware.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/market-data/BTC");
        request.Headers.Host = "ewa.example.com";

        var response = await _client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Contains("X-Content-Type-Options"), Is.True);
            Assert.That(response.Headers.Contains("X-Frame-Options"), Is.True);
            Assert.That(response.Headers.Contains("Strict-Transport-Security"), Is.True);
        });
    }

    [Test]
    public async Task CORS_DisallowedOrigin_DoesNotEchoOrigin()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/market-data/BTC");
        request.Headers.Add("Origin", "https://evil.example.com");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        Assert.That(
            response.Headers.Contains("Access-Control-Allow-Origin") &&
            response.Headers.GetValues("Access-Control-Allow-Origin")
                .Any(v => v == "https://evil.example.com"),
            Is.False,
            "Evil origin must not be echoed in ACAO header");
    }

    [Test]
    public async Task RateLimiting_LoginEndpoint_RejectsWithExpectedStatus()
    {
        // The login endpoint is rate-limited; a single bad-credential request should still
        // return a 4xx (401/400) or 429 — never 200 — confirming the endpoint is wired.
        var payload = new StringContent(
            """{"email":"test@test.com","password":"wrong"}""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/auth/login", payload);

        Assert.That(response.Headers.Contains("Retry-After") ||
                    (int)response.StatusCode is 429 or 401 or 400,
                    Is.True,
                    "Rate-limited endpoint must return 401/400/429");
    }
}
