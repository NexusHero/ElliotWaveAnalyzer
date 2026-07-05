using Microsoft.Extensions.AI;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Builds a fully-wired <see cref="IChatClient"/> for a provider from an arbitrary API key — the one
/// place the provider-SDK construction (OpenAI / Gemini / Claude) lives, reused both by the startup
/// keyed registrations and by the per-user client (REQ-013). Internal to Infrastructure so the SDK
/// types never leak upward.
/// </summary>
internal interface IUserChatClientFactory
{
    /// <summary>Creates a chat client for <paramref name="provider"/> ("gemini"/"claude"/"openai") using <paramref name="apiKey"/>.</summary>
    IChatClient Create(string provider, string apiKey);
}
