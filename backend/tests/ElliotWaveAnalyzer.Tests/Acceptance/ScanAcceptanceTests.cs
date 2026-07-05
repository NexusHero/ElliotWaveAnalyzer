using System.Net;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the setup scanner (<c>GET /api/scan</c>): a sweep over the faked universe
/// returns a well-formed ranked result over the full stack (auth, market-data fakes, the deterministic
/// pipeline); unauthenticated → 401.
/// </summary>
[TestFixture]
public sealed class ScanAcceptanceTests
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
    public async Task Scan_OverTheFakeUniverse_ReturnsARankedResult()
    {
        var response = await _client.GetAsync("/api/scan?symbols=BTC,ETH&timeframe=1D&limit=10");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"body: {body}");
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(root.GetProperty("scanned").GetInt32(), Is.EqualTo(2));
            Assert.That(root.GetProperty("hits").ValueKind, Is.EqualTo(JsonValueKind.Array));
        });
    }

    [Test]
    public async Task Scan_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/scan");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
