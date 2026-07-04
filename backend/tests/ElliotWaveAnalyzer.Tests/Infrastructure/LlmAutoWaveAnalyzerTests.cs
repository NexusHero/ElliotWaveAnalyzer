using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for the single-provider <see cref="LlmAutoWaveAnalyzer"/>: it ranks candidates via
/// the active <see cref="Microsoft.Extensions.AI.IChatClient"/> and reports the active provider's
/// name. The chat boundary is a <see cref="FakeChatClient"/> returning canned ranking JSON.
/// </summary>
[TestFixture]
public sealed class LlmAutoWaveAnalyzerTests
{
    private static readonly DateTime Day = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static WaveCandidate Candidate(int id) =>
        new(
            id,
            "Impulse",
            new WaveAnnotation(Day, 100m, "1"),
            [
                new WaveAnnotation(Day.AddDays(1), 120m, "1"),
                new WaveAnnotation(Day.AddDays(2), 110m, "2"),
                new WaveAnnotation(Day.AddDays(3), 150m, "3"),
                new WaveAnnotation(Day.AddDays(4), 130m, "4"),
                new WaveAnnotation(Day.AddDays(5), 170m, "5"),
            ],
            new WaveRuleReport(true, [], []),
            null);

    private const string RankingJson =
        """
        {
          "bestCandidateId": 1,
          "marketSummary": "single provider view",
          "rankings": [
            { "candidateId": 0, "confidence": "low", "rationale": "r0", "outlook": "o0" },
            { "candidateId": 1, "confidence": "high", "rationale": "r1", "outlook": "o1" }
          ]
        }
        """;

    private static LlmAutoWaveAnalyzer Build(string responseJson)
    {
        var options = new LlmProviderOptions
        {
            Active = "Gemini",
            Gemini = new LlmEndpointOptions { ApiKey = "g-key", Model = "gemini-model" },
        };
        return new LlmAutoWaveAnalyzer(
            new FakeChatClient { ResponseJson = responseJson },
            Options.Create(options),
            NullLogger<LlmAutoWaveAnalyzer>.Instance);
    }

    [Test]
    public void ProviderName_IsTheActiveProvider()
        => Assert.That(Build(RankingJson).ProviderName, Is.EqualTo("Gemini"));

    [Test]
    public async Task RankAsync_ReturnsTheClientRankingAndUsage()
    {
        var result = await Build(RankingJson).RankAsync("BTC", [], [Candidate(0), Candidate(1)]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Ranking.BestCandidateId, Is.EqualTo(1));
            Assert.That(result.Ranking.MarketSummary, Does.Contain("single provider view"));
            Assert.That(result.Usage.TotalTokens, Is.EqualTo(150)); // FakeChatClient default
            Assert.That(result.Usage.Provider, Does.Contain("Gemini"));
        });
    }
}
