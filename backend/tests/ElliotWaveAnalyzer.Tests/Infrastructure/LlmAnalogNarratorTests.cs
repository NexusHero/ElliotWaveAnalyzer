using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Tests.Acceptance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="LlmAnalogNarrator"/>: no-key and insufficient-history degradation, the fact-guard
/// rejecting a hallucinated rate, and the happy path passing a grounded summary through.
/// </summary>
[TestFixture]
public sealed class LlmAnalogNarratorTests
{
    private static readonly SetupFeatures Features =
        new(StructureKind.Impulse, true, "1d", 0.7, 0.5, 2.0, 0.08, 0.55, 0.6);

    // 25 concluded analogs, 17 reached target (68%), median 12 days; sufficient.
    private static AnalogReport SufficientReport()
    {
        var formed = new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var analogs = Enumerable.Range(0, 5)
            .Select(i => new HistoricalAnalog(
                new HistoricalSetup("SYM", formed, formed.AddDays(10 + i), AnalysisOutcome.TargetReached, Features),
                0.9))
            .ToList();
        var stats = new AnalogStats(25, 17, 8, 0.68, 12.0, Sufficient: true);
        return new AnalogReport(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero), analogs, stats);
    }

    private static LlmAnalogNarrator Narrator(params IChatClient[] clients) =>
        new(clients, Options.Create(new LlmProviderOptions()), new FakeNarrativeLanguageProvider(),
            NullLogger<LlmAnalogNarrator>.Instance);

    [Test]
    public async Task NarrateAsync_NoChatClient_DegradesWithReason()
    {
        var result = await Narrator().NarrateAsync(SufficientReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("No LLM provider"));
        });
    }

    [Test]
    public async Task NarrateAsync_InsufficientHistory_IsNotSummarised()
    {
        var report = SufficientReport() with { };
        var thin = new AnalogReport(report.AsOf, report.Analogs, new AnalogStats(2, 1, 1, 0.5, 8.0, Sufficient: false));

        var result = await Narrator(new StubChatClient("""{ "narrative": "x" }""")).NarrateAsync(thin);

        Assert.That(result.NarrativeUnavailableReason, Does.Contain("Not enough historical analogs"));
    }

    [Test]
    public async Task NarrateAsync_GermanPreference_AppendsTheLanguageDirectiveToTheSystemPrompt()
    {
        var chat = new FakeChatClient { ResponseJson = """{ "narrative": "x" }""" };
        var narrator = new LlmAnalogNarrator(
            [chat], Options.Create(new LlmProviderOptions()),
            new FakeNarrativeLanguageProvider { Language = NarrativeLanguage.German },
            NullLogger<LlmAnalogNarrator>.Instance);

        await narrator.NarrateAsync(SufficientReport());

        var systemMessage = chat.LastMessages!.First(m => m.Role == ChatRole.System);
        Assert.That(systemMessage.Text, Does.Contain("German"));
    }

    [Test]
    public async Task NarrateAsync_FactCleanNarrative_IsReturned()
    {
        var client = new StubChatClient("""{ "narrative": "The analogs skew constructive." }""");

        var result = await Narrator(client).NarrateAsync(SufficientReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Does.Contain("constructive"));
            Assert.That(result.NarrativeUnavailableReason, Is.Null);
        });
    }

    [Test]
    public async Task NarrateAsync_GroundedNumbers_AreReturned()
    {
        var client = new StubChatClient("""{ "narrative": "68% of the 25 analogs reached target." }""");

        var result = await Narrator(client).NarrateAsync(SufficientReport());

        Assert.That(result.Narrative, Does.Contain("68%"));
    }

    [Test]
    public async Task NarrateAsync_HallucinatedRate_IsRejectedByTheFactGuard()
    {
        var client = new StubChatClient("""{ "narrative": "A stellar 95% reached target." }""");

        var result = await Narrator(client).NarrateAsync(SufficientReport());

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.NarrativeUnavailableReason, Does.Contain("fact check"));
        });
    }

    [Test]
    public async Task NarrateAsync_UnparseableResponse_DegradesWithReason()
    {
        var result = await Narrator(new StubChatClient("not json")).NarrateAsync(SufficientReport());

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
