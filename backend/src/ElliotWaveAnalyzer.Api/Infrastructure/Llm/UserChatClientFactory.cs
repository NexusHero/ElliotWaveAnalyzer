using System.ClientModel;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Constructs a provider chat client from a given API key and wraps it in the same middleware
/// (distributed cache + logging) as the startup clients. The model per provider comes from options;
/// only the key varies, so the operator's startup key and a user's stored key build identical clients.
/// </summary>
internal sealed class UserChatClientFactory(
    IOptions<LlmProviderOptions> options,
    IDistributedCache cache,
    ILoggerFactory loggerFactory) : IUserChatClientFactory
{
    /// <inheritdoc/>
    public IChatClient Create(string provider, string apiKey)
    {
        // An empty key still constructs a client (it fails later with the provider's auth error, as
        // before) — the per-user path only calls this with a non-empty key anyway.
        var opts = options.Value;
        IChatClient inner = provider.Trim().ToLowerInvariant() switch
        {
            "openai" => new OpenAIClient(apiKey).GetChatClient(opts.OpenAI.Model).AsIChatClient(),
            "gemini" => new ChatClient(
                    model: opts.Gemini.Model,
                    credential: new ApiKeyCredential(apiKey),
                    options: new OpenAIClientOptions
                    {
                        Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/"),
                    })
                .AsIChatClient(),
            "claude" => new AnthropicClient(apiKey).Messages,
            var other => throw new InvalidOperationException($"Unknown LLM provider '{other}'."),
        };

        return new ChatClientBuilder(inner)
            .UseDistributedCache(cache)
            .UseLogging(loggerFactory)
            .Build();
    }
}
