using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

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

        // The provider-SDK construction lives in one factory (REQ-013), reused by both the startup
        // keyed clients (below) and the per-user client. HttpContextAccessor lets the per-user client
        // read the calling user from the request.
        services.AddHttpContextAccessor();
        services.AddSingleton<IUserChatClientFactory, UserChatClientFactory>();

        // One keyed IChatClient per provider (.NET 8 Keyed Services), built from the operator's
        // startup key. The factory for a provider only runs when that provider is resolved, so unused
        // providers cost nothing. Adding a provider = one more keyed registration (OCP).
        services.AddKeyedSingleton<IChatClient>("openai", (sp, _) =>
            KeyedStartupClient(sp, "openai", o => o.OpenAI.ApiKey));
        services.AddKeyedSingleton<IChatClient>("gemini", (sp, _) =>
            KeyedStartupClient(sp, "gemini", o => o.Gemini.ApiKey));
        services.AddKeyedSingleton<IChatClient>("claude", (sp, _) =>
            KeyedStartupClient(sp, "claude", o => o.Claude.ApiKey));

        // The single non-keyed IChatClient is the active provider resolved PER REQUEST against the
        // calling user's stored key, falling back to the startup key (REQ-013). Every consumer that
        // injects IChatClient (manual analysis, single-provider ranking, narrative, vision) honours it.
        services.AddSingleton<IChatClient, UserAwareChatClient>();

        // Resolver seam so consumers depend on an abstraction instead of IServiceProvider
        // (the keyed-service lookup lives only in KeyedChatClientResolver).
        services.AddSingleton<IChatClientResolver, KeyedChatClientResolver>();

        services.AddTransient<ILlmWaveAnalyzer, LlmWaveAnalyzer>();

        // Narrative language (#228): resolves the calling user's preference from their session, for
        // every narrator that doesn't already receive a userId parameter. Scoped (reads AppDbContext
        // via INarrativeLanguageSettingsService, registered by AddAppAuth).
        services.AddScoped<INarrativeLanguageProvider, HttpContextNarrativeLanguageProvider>();

        // Full-auto ranking: a consensus across all keyed providers when LlmProvider:Ensemble
        // is true, otherwise just the active provider. Chosen once at startup from config.
        var ensemble = configuration.GetValue<bool>($"{LlmProviderOptions.SectionName}:Ensemble");
        if (ensemble)
        {
            services.AddTransient<IAutoWaveAnalyzer, EnsembleAutoWaveAnalyzer>();
        }
        else
        {
            services.AddTransient<IAutoWaveAnalyzer, LlmAutoWaveAnalyzer>();
        }

        // Token tracking (singleton — accumulates across requests).
        // In-memory per instance; see InMemoryTokenTracker for the distributed seam.
        services.AddSingleton<ITokenTracker, InMemoryTokenTracker>();

        // Calibrated, self-weighting analyst panel (#184): runs every catalog persona over the
        // same IChatClient seam. Scoped because it depends on the scoped calibration provider
        // (touches AppDbContext), unlike the single-shot rankers above.
        services.AddScoped<IPersonaAnalystPanel, PersonaAnalystPanel>();

        // Portfolio-review narratives (fact-checked; degrades to null without a key).
        services.AddScoped<IPositionNarrator, LlmPositionNarrator>();

        // Historical-analog summaries (REQ-034): fact-guarded; degrades to a reason without a key.
        services.AddScoped<IAnalogNarrator, LlmAnalogNarrator>();

        // Socionomics summaries (REQ-038): fact-guarded; degrades to a reason without a key or coverage.
        services.AddScoped<ISentimentNarrator, LlmSentimentNarrator>();

        // Alternate-hypothesis proposals (REQ-035): the LLM only names structures to test; the engine
        // validates them. Absent (feature off) when no key is configured.
        services.AddScoped<IHypothesisProposer, LlmHypothesisProposer>();

        // Vision import (REQ-028): a vision LLM extracts a claimed count; deterministic pipeline verifies it.
        services.AddScoped<IChartVisionExtractor, LlmChartVisionExtractor>();
        services.AddScoped<IImageVerificationService, ImageVerificationService>();

        return services;

        // A keyed startup client for one provider, built from the operator's configured key via the
        // shared factory (same construction + middleware as the per-user client).
        static IChatClient KeyedStartupClient(
            IServiceProvider sp, string provider, Func<LlmProviderOptions, string> apiKey)
        {
            var options = sp.GetRequiredService<IOptions<LlmProviderOptions>>().Value;
            return sp.GetRequiredService<IUserChatClientFactory>().Create(provider, apiKey(options));
        }
    }
}
