using System.Net.Http.Json;
using System.Text.Json;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>
/// The headline invariant I1 — <b>the LLM never does geometry</b> — proven end to end: swapping the
/// model's <i>entire</i> output leaves every deterministic field (the rule report and the forward
/// levels) byte-identical, while only the LLM's own coaching assessment changes. If a model could move
/// the geometry, this test would fail.
/// </summary>
[TestFixture]
public sealed class LlmSwapInvarianceAcceptanceTests
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

    private static object AnalysisRequest() => new
    {
        symbol = "BTC",
        annotations = new[]
        {
            new { date = "2024-01-05T00:00:00Z", price = 38_000m, label = "1" },
            new { date = "2024-01-15T00:00:00Z", price = 35_000m, label = "2" },
            new { date = "2024-02-01T00:00:00Z", price = 52_000m, label = "3" },
        },
    };

    private async Task<JsonElement> AnalyzeWithLlmOutputAsync(string responseJson)
    {
        _factory.Chat.ResponseJson = responseJson;
        var response = await _client.PostAsJsonAsync("/api/wave-analysis", AnalysisRequest());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Test]
    public async Task SwappingTheEntireLlmOutput_LeavesTheDeterministicFieldsByteIdentical()
    {
        var first = await AnalyzeWithLlmOutputAsync(
            """
            { "isValid": true, "violations": [], "warnings": ["Wave 2 retracement is shallow"],
              "analysis": "A clean five-wave impulse.", "confidence": "high" }
            """);

        var second = await AnalyzeWithLlmOutputAsync(
            """
            { "isValid": false, "violations": ["Wave 3 is the shortest"], "warnings": [],
              "analysis": "This count looks broken to me.", "confidence": "low" }
            """);

        Assert.Multiple(() =>
        {
            // The geometry is untouched by the model: rule report and levels are byte-identical.
            Assert.That(
                second.GetProperty("ruleReport").GetRawText(),
                Is.EqualTo(first.GetProperty("ruleReport").GetRawText()));
            Assert.That(
                second.GetProperty("levels").GetRawText(),
                Is.EqualTo(first.GetProperty("levels").GetRawText()));

            // Sanity: the LLM output genuinely changed, so the invariance above is meaningful.
            Assert.That(
                second.GetProperty("result").GetProperty("analysis").GetString(),
                Is.Not.EqualTo(first.GetProperty("result").GetProperty("analysis").GetString()));
        });
    }
}
