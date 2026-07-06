using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// End-to-end acceptance for <c>GET /api/wave-analysis/hypotheses</c> (#186). Drives the API with faked
/// market data and a faked chat client, so routing, pivot detection, the vocabulary guard, the
/// deterministic validation and serialization are all exercised. The LLM proposes; the engine validates.
/// </summary>
[TestFixture]
public sealed class AlternateHypothesesAcceptanceTests
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
    public async Task Hypotheses_DropsOutOfVocabAndValidatesTheRest()
    {
        // The model proposes one in-vocabulary structure and one made-up one; only the former is tested.
        _factory.Chat.ResponseJson =
            """{ "proposals": [ { "structure": "impulse", "reason": "clean five up" }, { "structure": "combination", "reason": "invented" } ] }""";

        var response = await _client.GetAsync("/api/wave-analysis/hypotheses?symbol=BTC");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var validated = body.GetProperty("validated");
        var rejected = body.GetProperty("rejected");

        Assert.Multiple(() =>
        {
            Assert.That(body.GetProperty("symbol").GetString(), Is.EqualTo("BTC"));
            Assert.That(body.GetProperty("unavailable").ValueKind, Is.EqualTo(JsonValueKind.Null));
            // Exactly one verdict (impulse); the out-of-vocab "combination" never reached the engine.
            Assert.That(validated.GetArrayLength() + rejected.GetArrayLength(), Is.EqualTo(1));
            foreach (var h in validated.EnumerateArray().Concat(rejected.EnumerateArray()))
            {
                Assert.That(h.GetProperty("structure").GetString(), Is.EqualTo("Impulse"));
            }
        });
    }

    [Test]
    public async Task Hypotheses_UnsupportedSymbol_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/wave-analysis/hypotheses?symbol=ZZZ");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Hypotheses_InvalidInterval_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/wave-analysis/hypotheses?symbol=BTC&interval=5m");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
