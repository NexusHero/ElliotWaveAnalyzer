using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="LlmHypothesisProposer"/>: off with no key, parses the proposal JSON, and caps the number
/// returned (#186, AC5/AC6). It only names structures — validity is decided downstream by the engine.
/// </summary>
[TestFixture]
public sealed class LlmHypothesisProposerTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<SwingPivot> Pivots(int n) =>
        Enumerable.Range(0, n).Select(i => new SwingPivot(Start.AddDays(i * 5), 100 + i * 10, i % 2 == 1)).ToList();

    private static LlmHypothesisProposer Proposer(params IChatClient[] clients) =>
        new(clients, Options.Create(new LlmProviderOptions()), NullLogger<LlmHypothesisProposer>.Instance);

    [Test]
    public void IsConfigured_ReflectsWhetherAChatClientIsPresent()
    {
        Assert.That(Proposer().IsConfigured, Is.False);
        Assert.That(Proposer(new StubChatClient("{}")).IsConfigured, Is.True);
    }

    [Test]
    public async Task ProposeAsync_NoChatClient_ReturnsEmpty()
    {
        var result = await Proposer().ProposeAsync("BTC", Pivots(6), 5);
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ProposeAsync_ParsesStructureAndReason()
    {
        var client = new StubChatClient(
            """{ "proposals": [ { "structure": "zigzag", "reason": "sharp abc" }, { "structure": "triangle", "reason": "coiling" } ] }""");

        var result = await Proposer(client).ProposeAsync("BTC", Pivots(6), 5);

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result[0].Structure, Is.EqualTo("zigzag"));
            Assert.That(result[0].Reason, Is.EqualTo("sharp abc"));
        });
    }

    [Test]
    public async Task ProposeAsync_CapsAtMax()
    {
        var many = string.Join(",", Enumerable.Range(0, 9).Select(_ => """{ "structure": "flat", "reason": "x" }"""));
        var client = new StubChatClient($$"""{ "proposals": [ {{many}} ] }""");

        var result = await Proposer(client).ProposeAsync("BTC", Pivots(6), 3);

        Assert.That(result, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ProposeAsync_UnparseableOrTooFewPivots_ReturnsEmpty()
    {
        Assert.That(await Proposer(new StubChatClient("not json")).ProposeAsync("BTC", Pivots(6), 5), Is.Empty);
        Assert.That(await Proposer(new StubChatClient("{}")).ProposeAsync("BTC", Pivots(2), 5), Is.Empty);
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
