using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>GET /api/symbols/search</c>: a query resolves to instruments,
/// a blank query is rejected, and auth is required. The resolver is faked (no Yahoo network).
/// </summary>
[TestFixture]
public sealed class SymbolSearchAcceptanceTests
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
    public async Task Search_ValidQuery_Returns200WithInstruments()
    {
        var response = await _client.GetAsync("/api/symbols/search?q=apple");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Multiple(() =>
        {
            Assert.That(body.GetArrayLength(), Is.GreaterThan(0));
            Assert.That(body[0].GetProperty("symbol").GetString(), Is.EqualTo("AAPL"));
            Assert.That(body[0].GetProperty("assetClass").GetString(), Is.EqualTo("EQUITY"));
        });
    }

    [Test]
    public async Task Search_BlankQuery_Returns400()
    {
        var response = await _client.GetAsync("/api/symbols/search?q=%20");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Search_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.GetAsync("/api/symbols/search?q=apple");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
