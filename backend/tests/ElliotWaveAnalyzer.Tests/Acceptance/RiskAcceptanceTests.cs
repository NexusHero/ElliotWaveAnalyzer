using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for the risk layer (<c>POST /api/risk</c>): the geometry + account-risk go in
/// over the full stack (auth, JSON source-gen, the pure calculator) and a well-formed assessment comes
/// back; an entry on the wrong side of the invalidation returns an explicit no-valid-stop result (still
/// 200); unauthenticated → 401.
/// </summary>
[TestFixture]
public sealed class RiskAcceptanceTests
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
    public async Task Assess_ValidLong_ReturnsStopRewardAndSize()
    {
        var response = await _client.PostAsJsonAsync("/api/risk", new
        {
            entry = 100m,
            invalidation = 90m,
            targets = new[] { 130m },
            bullish = true,
            accountEquity = 10_000m,
            riskPercent = 1m,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("hasValidStop").GetBoolean(), Is.True);
            Assert.That(body.GetProperty("stopDistanceAbs").GetDecimal(), Is.EqualTo(10m));
            Assert.That(body.GetProperty("suggestedSize").GetDecimal(), Is.EqualTo(10m));
            Assert.That(body.GetProperty("targets")[0].GetProperty("rewardToRisk").GetDecimal(), Is.EqualTo(3.0m));
        });
    }

    [Test]
    public async Task Assess_EntryOnWrongSide_ReturnsNoValidStop()
    {
        var response = await _client.PostAsJsonAsync("/api/risk", new
        {
            entry = 100m,
            invalidation = 105m,
            targets = new[] { 130m },
            bullish = true,
            riskAmount = 100m,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("hasValidStop").GetBoolean(), Is.False);
            Assert.That(body.GetProperty("noStopReason").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(body.GetProperty("suggestedSize").ValueKind, Is.EqualTo(JsonValueKind.Null), "no size without a valid stop");
        });
    }

    [Test]
    public async Task Assess_Unauthenticated_Returns401()
    {
        using var anon = _factory.CreateClient();

        var response = await anon.PostAsJsonAsync("/api/risk", new
        {
            entry = 100m,
            invalidation = 90m,
            targets = new[] { 130m },
            bullish = true,
            riskAmount = 100m,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
