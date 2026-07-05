using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance tests for <c>GET /api/wave-analysis/topdown</c>. Drives the running API
/// over HTTP with faked market data (daily + intraday), so routing, provider selection,
/// resampling, pivot detection, the pure top-down analyzer and serialization are all exercised.
/// BTC is served by both the daily and intraday fakes, so the full weekly→daily→4H ladder resolves.
/// </summary>
[TestFixture]
public sealed class TopDownAnalysisAcceptanceTests
{
    private static readonly string[] Verdicts = ["Consistent", "Tension", "Contradiction"];

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
    public async Task TopDown_SupportedSymbol_ReturnsChainAcrossTimeframesWithVerdicts()
    {
        var response = await _client.GetAsync("/api/wave-analysis/topdown?symbol=BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var timeframes = body.GetProperty("timeframes");
        var links = body.GetProperty("links");

        Assert.Multiple(() =>
        {
            // BTC is served on daily (1W, 1D) and intraday (4H) → the full three-rung ladder.
            Assert.That(timeframes.GetArrayLength(), Is.EqualTo(3));
            Assert.That(timeframes[0].GetProperty("interval").GetString(), Is.EqualTo("1W"));
            Assert.That(timeframes[2].GetProperty("interval").GetString(), Is.EqualTo("4H"));

            // Every link carries one of the three verdicts and a non-empty reason.
            Assert.That(links.GetArrayLength(), Is.GreaterThanOrEqualTo(1));
            foreach (var link in links.EnumerateArray())
            {
                Assert.That(Verdicts, Does.Contain(link.GetProperty("verdict").GetString()));
                Assert.That(link.GetProperty("reason").GetString(), Is.Not.Empty);
            }

            // The summary chains the timeframes (three rungs → two arrows).
            var summary = body.GetProperty("summary").GetString()!;
            Assert.That(summary, Does.Contain("1W:"));
            Assert.That(summary.Split('→'), Has.Length.EqualTo(3));
        });
    }

    [Test]
    public async Task TopDown_MalformedSymbol_Returns400()
    {
        var response = await _client.GetAsync("/api/wave-analysis/topdown?symbol=%20bad%20");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task TopDown_RequiresAuthentication()
    {
        using var anon = _factory.CreateClient();
        var response = await anon.GetAsync("/api/wave-analysis/topdown?symbol=BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
