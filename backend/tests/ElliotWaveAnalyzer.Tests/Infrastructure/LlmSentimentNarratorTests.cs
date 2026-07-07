using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="LlmSentimentNarrator"/>: no-key and no-coverage degradation, the fact-guard rejecting a
/// hallucinated mood score, and the happy path passing a grounded summary through.
/// </summary>
[TestFixture]
public sealed class LlmSentimentNarratorTests
{
    // Series readings 0.80 / 0.50; one bearish divergence at wave "5" citing the same figures.
    private static SentimentReport CoveredReport()
    {
        var series = new[]
        {
            new SentimentPoint(new DateTime(2024, 1, 10), 0.80),
            new SentimentPoint(new DateTime(2024, 1, 20), 0.50),
        };
        var divergences = new[]
        {
            new MoodDivergence("5", new DateTime(2024, 1, 20), MoodDivergenceKind.Bearish, 0.80, 0.50),
        };
        return new SentimentReport(true, series, divergences);
    }

    private static LlmSentimentNarrator Narrator(params IChatClient[] clients) =>
        new(clients, Options.Create(new LlmProviderOptions()), NullLogger<LlmSentimentNarrator>.Instance);

    [Test]
    public async Task NarrateAsync_NoChatClient_DegradesWithReason()
    {
        var result = await Narrator().NarrateAsync(CoveredReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("No LLM provider"));
        });
    }

    [Test]
    public async Task NarrateAsync_NoCoverage_IsNotSummarised()
    {
        var noCoverage = SentimentReport.NoCoverage("No sentiment provider is configured for this symbol.");

        var result = await Narrator(new StubChatClient("""{ "narrative": "x" }""")).NarrateAsync(noCoverage);

        Assert.That(result.NarrativeUnavailableReason, Does.Contain("No sentiment coverage"));
    }

    [Test]
    public async Task NarrateAsync_FactCleanNarrative_IsReturned()
    {
        var client = new StubChatClient("""{ "narrative": "Mood is fading into the advance." }""");

        var result = await Narrator(client).NarrateAsync(CoveredReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Does.Contain("fading"));
            Assert.That(result.NarrativeUnavailableReason, Is.Null);
        });
    }

    [Test]
    public async Task NarrateAsync_GroundedMoodScores_AreReturned()
    {
        var client = new StubChatClient("""{ "narrative": "Mood peaked at 0.80 but only reached 0.50." }""");

        var result = await Narrator(client).NarrateAsync(CoveredReport());

        Assert.That(result.Narrative, Does.Contain("0.80"));
    }

    [Test]
    public async Task NarrateAsync_HallucinatedMoodScore_IsRejectedByTheFactGuard()
    {
        var client = new StubChatClient("""{ "narrative": "An extreme reading of 0.99 was seen." }""");

        var result = await Narrator(client).NarrateAsync(CoveredReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("fact check"));
        });
    }

    [Test]
    public async Task NarrateAsync_UnparseableResponse_DegradesWithReason()
    {
        var result = await Narrator(new StubChatClient("not json")).NarrateAsync(CoveredReport());

        Assert.That(result.NarrativeUnavailableReason, Does.Contain("no usable text"));
    }

    private sealed class StubChatClient(string response) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, response)));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
