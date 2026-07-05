using Microsoft.Extensions.AI;

namespace ElliotWaveAnalyzer.Tests.Acceptance;

/// <summary>Deterministic <see cref="IChatClient"/> returning a configurable canned response.</summary>
public sealed class FakeChatClient : IChatClient
{
    /// <summary>Raw assistant text the analyzer will parse. Defaults to a valid wave assessment.</summary>
    public string ResponseJson { get; set; } =
        """
        { "isValid": true, "violations": [], "warnings": ["Wave 2 retracement is shallow"],
          "analysis": "A clean five-wave impulse.", "confidence": "high" }
        """;

    public UsageDetails Usage { get; set; } = new()
    {
        InputTokenCount = 100,
        OutputTokenCount = 50,
        TotalTokenCount = 150,
    };

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, ResponseJson))
        {
            Usage = Usage,
        });

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming is not used by the wave analyzer.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
