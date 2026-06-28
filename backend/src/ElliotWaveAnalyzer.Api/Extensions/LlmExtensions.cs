using System.ClientModel;
using Anthropic.SDK;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace ElliotWaveAnalyzer.Api.Extensions;

/// <summary>
/// LLM provider wiring. The active provider is selected via LlmProvider:Active in
/// appsettings.json. We register a single IChatClient (Microsoft.Extensions.AI) for that
/// provider; the provider-agnostic LlmWaveAnalyzer consumes it. Adding/switching providers
/// means editing only this factory — no bespoke HTTP/JSON/token code (OCP).
/// </summary>
internal static class LlmExtensions
{
    internal static IServiceCollection AddLlmServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<LlmProviderOptions>()
            .BindConfiguration(LlmProviderOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // One keyed IChatClient per provider (.NET 8 Keyed Services). The factory for a
        // provider only runs when that provider is selected, so unused providers cost nothing
        // and need no credentials. Adding a provider = one more keyed registration (OCP).
        services.AddKeyedSingleton<IChatClient>("openai", (sp, _) =>
        {
            var endpoint = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.OpenAI;
            // OpenAI: native endpoint.
            var inner = new OpenAIClient(endpoint.ApiKey)
                .GetChatClient(endpoint.Model)
                .AsIChatClient();
            return BuildClient(inner, sp);
        });

        services.AddKeyedSingleton<IChatClient>("gemini", (sp, _) =>
        {
            var endpoint = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.Gemini;
            // Gemini: Google exposes an OpenAI-compatible endpoint, so the same client works
            // by pointing it at that base URL.
            var inner = new ChatClient(
                    model: endpoint.Model,
                    credential: new ApiKeyCredential(endpoint.ApiKey),
                    options: new OpenAIClientOptions
                    {
                        Endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta/openai/")
                    })
                .AsIChatClient();
            return BuildClient(inner, sp);
        });

        services.AddKeyedSingleton<IChatClient>("claude", (sp, _) =>
        {
            var endpoint = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.Claude;
            // Claude: Anthropic.SDK exposes its Messages endpoint as an IChatClient.
            var inner = new AnthropicClient(endpoint.ApiKey).Messages;
            return BuildClient(inner, sp);
        });

        // The single non-keyed IChatClient resolves the active provider by key. An unknown
        // value surfaces naturally as InvalidOperationException from the keyed lookup.
        services.AddSingleton<IChatClient>(sp =>
        {
            var key = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value.Active.ToLowerInvariant();
            return sp.GetRequiredKeyedService<IChatClient>(key);
        });

        services.AddTransient<ILlmWaveAnalyzer, LlmWaveAnalyzer>();
        services.AddTransient<IAutoWaveAnalyzer, LlmAutoWaveAnalyzer>();

        // Token tracking (singleton — accumulates across requests).
        // In-memory per instance; see InMemoryTokenTracker for the distributed seam.
        services.AddSingleton<ITokenTracker, InMemoryTokenTracker>();

        return services;

        // Standard middleware pipeline. Distributed caching short-circuits identical requests
        // (same prompt → cached response, saving latency and token spend); OpenTelemetry/retry
        // can be chained the same way without touching provider code.
        static IChatClient BuildClient(IChatClient inner, IServiceProvider sp)
            => new ChatClientBuilder(inner)
                .UseDistributedCache(sp.GetRequiredService<IDistributedCache>())
                .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                .Build();
    }
}
