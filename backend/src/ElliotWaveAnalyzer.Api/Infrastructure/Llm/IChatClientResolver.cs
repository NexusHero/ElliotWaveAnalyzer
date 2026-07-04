using Microsoft.Extensions.AI;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Resolves a keyed <see cref="IChatClient"/> by provider key ("gemini" / "claude" / "openai").
/// A thin seam over keyed DI so consumers (e.g. <see cref="EnsembleAutoWaveAnalyzer"/>) depend on
/// this abstraction instead of injecting <see cref="IServiceProvider"/> and doing service-locator
/// lookups themselves (Dependency Inversion). Kept internal to Infrastructure so the third-party
/// <see cref="IChatClient"/> type never leaks into the Application/Interfaces layers.
/// </summary>
internal interface IChatClientResolver
{
    /// <summary>
    /// Returns the chat client registered under <paramref name="providerKey"/>. Throws if no
    /// client is registered for that key (e.g. a provider that was never configured).
    /// </summary>
    IChatClient Resolve(string providerKey);
}
