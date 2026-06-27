using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using static VerifyNUnit.Verifier;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Snapshot test that locks the full <see cref="WaveAnalysisResponse"/> shape — the LLM
/// assessment, the deterministic rule report, and token usage — so any unintended change to
/// the response contract is caught. The real service graph is wired with the deterministic
/// <see cref="FakeChatClient"/>, so the snapshot is stable.
/// </summary>
[TestFixture]
public sealed class WaveAnalysisSnapshotTests
{
    [Test]
    public async Task AnalyzeAsync_ValidWaves_MatchesSnapshot()
    {
        var options = Options.Create(new LlmProviderOptions { Active = "Gemini" });
        var chatClient = new FakeChatClient();
        var llm = new LlmWaveAnalyzer(chatClient, options, NullLogger<LlmWaveAnalyzer>.Instance);

        var tokenTracker = Substitute.For<ITokenTracker>();
        tokenTracker.IsBudgetExceeded().Returns(false);

        var providers = new IMarketDataProvider[] { new FakeMarketDataProvider() };
        var service = new WaveAnalysisService(
            providers, llm, tokenTracker, NullLogger<WaveAnalysisService>.Instance);

        var annotations = new List<WaveAnnotation>
        {
            new(new DateTime(2024, 1, 5, 0, 0, 0, DateTimeKind.Utc), 38_000m, "1"),
            new(new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), 35_000m, "2"),
            new(new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc), 52_000m, "3"),
        };

        var result = await service.ValidateAsync("BTC", annotations);

        await Verify(result);
    }
}
