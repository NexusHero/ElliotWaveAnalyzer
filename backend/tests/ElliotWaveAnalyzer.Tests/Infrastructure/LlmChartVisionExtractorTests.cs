using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// <see cref="LlmChartVisionExtractor"/>: valid JSON parses into a claim; malformed JSON throws after
/// exactly one retry (stub called twice); a missing vision model throws a clear error.
/// </summary>
[TestFixture]
public sealed class LlmChartVisionExtractorTests
{
    private const string ValidJson =
        """
        { "symbol": "ACME", "timeframe": "1D",
          "pivots": [
            { "approxDate": "2024-01-01", "approxPrice": 100.0, "label": "0" },
            { "approxDate": "2024-01-10", "approxPrice": 130.0, "label": "1" }
          ],
          "levels": [120.0], "zones": [{ "low": 180, "high": 190, "label": "target" }] }
        """;

    private static readonly byte[] Image = [0x89, 0x50, 0x4E, 0x47];

    private static LlmChartVisionExtractor Extractor(params IChatClient[] clients) =>
        new(clients, Options.Create(new LlmProviderOptions()), NullLogger<LlmChartVisionExtractor>.Instance);

    [Test]
    public async Task ExtractAsync_ValidJson_ParsesTheClaim()
    {
        var extraction = await Extractor(new CountingChatClient(ValidJson)).ExtractAsync(Image, "image/png");

        Assert.Multiple(() =>
        {
            Assert.That(extraction.Symbol, Is.EqualTo("ACME"));
            Assert.That(extraction.Pivots, Has.Count.EqualTo(2));
            Assert.That(extraction.Pivots[0].Label, Is.EqualTo("0"));
            Assert.That(extraction.Zones, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void ExtractAsync_MalformedJson_ThrowsAfterExactlyOneRetry()
    {
        var client = new CountingChatClient("not valid json");

        Assert.ThrowsAsync<ChartExtractionException>(() => Extractor(client).ExtractAsync(Image, "image/png"));
        Assert.That(client.Calls, Is.EqualTo(2), "initial attempt + one retry");
    }

    [Test]
    public async Task ExtractAsync_Retry_SendsCorrectiveFeedback()
    {
        // First attempt malformed, second attempt valid — the retry must carry a correction telling
        // the model its previous output was invalid, rather than resending the identical prompt.
        var client = new CountingChatClient("not valid json", ValidJson);

        var extraction = await Extractor(client).ExtractAsync(Image, "image/png");

        Assert.That(extraction.Pivots, Has.Count.EqualTo(2));
        Assert.That(client.Calls, Is.EqualTo(2));
        Assert.That(
            client.LastMessages.Any(m => m.Text.Contains("was not valid JSON", StringComparison.Ordinal)),
            Is.True,
            "the retry should include corrective feedback");
    }

    [Test]
    public void ExtractAsync_NoVisionClient_ThrowsInvalidOperation()
        => Assert.ThrowsAsync<InvalidOperationException>(() => Extractor().ExtractAsync(Image, "image/png"));

    /// <summary>
    /// Chat client that returns each supplied body in turn (repeating the last), counts calls, and
    /// records the messages of the most recent call so a test can assert on the retry's content.
    /// </summary>
    private sealed class CountingChatClient(params string[] responses) : IChatClient
    {
        public int Calls { get; private set; }

        public IReadOnlyList<ChatMessage> LastMessages { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastMessages = [.. messages];
            var body = responses[Math.Min(Calls, responses.Length - 1)];
            Calls++;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, body)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
