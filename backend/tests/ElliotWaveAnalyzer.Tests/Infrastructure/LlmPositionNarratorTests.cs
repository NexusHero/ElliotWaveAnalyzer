using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="LlmPositionNarrator"/>: graceful no-key degradation, the fact-guard rejecting a
/// hallucinated price, and the happy path passing a fact-clean narrative through.
/// </summary>
[TestFixture]
public sealed class LlmPositionNarratorTests
{
    private static readonly PositionBrief SampleBrief = new(
        "US0000000001", "ACME", "Acme Corp", "1W: Impulse → 1D: Zigzag", Bullish: true,
        CurrentPrice: 150.00m,
        Invalidation: new PriceLevel(120.00m, LevelSide.Below, "inv", "end of 1"),
        EntryZone: new PriceZone(130.00m, 135.00m, "entry", "fib"),
        TargetZones: [new PriceZone(180.00m, 190.00m, "target", "ext")],
        Scale: FibScale.Linear);

    private static LlmPositionNarrator Narrator(params IChatClient[] clients) =>
        new(clients, Options.Create(new LlmProviderOptions()), NullLogger<LlmPositionNarrator>.Instance);

    [Test]
    public async Task NarrateAsync_NoChatClient_DegradesWithReason()
    {
        var result = await Narrator().NarrateAsync(SampleBrief);

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.UnavailableReason, Does.Contain("No LLM provider"));
        });
    }

    [Test]
    public async Task NarrateAsync_FactCleanNarrative_IsReturned()
    {
        var client = new StubChatClient("""{ "narrative": "Holds above 120.00; a dip to 130.00 favors 180.00." }""");

        var result = await Narrator(client).NarrateAsync(SampleBrief);

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Does.Contain("Holds above"));
            Assert.That(result.UnavailableReason, Is.Null);
        });
    }

    [Test]
    public async Task NarrateAsync_HallucinatedPrice_IsRejectedByTheFactGuard()
    {
        var client = new StubChatClient("""{ "narrative": "This is heading straight to 999.99." }""");

        var result = await Narrator(client).NarrateAsync(SampleBrief);

        Assert.Multiple(() =>
        {
            Assert.That(result.Narrative, Is.Null);
            Assert.That(result.UnavailableReason, Does.Contain("fact check"));
        });
    }

    [Test]
    public async Task NarrateAsync_UnparseableResponse_DegradesWithReason()
    {
        var result = await Narrator(new StubChatClient("not json at all")).NarrateAsync(SampleBrief);
        Assert.That(result.Narrative, Is.Null);
    }

    /// <summary>Minimal deterministic <see cref="IChatClient"/> returning a fixed assistant message.</summary>
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
