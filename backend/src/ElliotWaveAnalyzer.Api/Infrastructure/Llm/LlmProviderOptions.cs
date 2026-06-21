namespace ElliotWaveAnalyzer.Api.Infrastructure.Llm;

/// <summary>
/// Unified configuration for all LLM providers.
/// Bound from <c>appsettings.json → LlmProvider</c>.
///
/// WHY one class instead of separate GeminiOptions / ClaudeOptions:
/// The provider is switched at runtime via <see cref="Active"/>.
/// Having all options co-located makes it obvious which keys exist and avoids
/// scattered configuration sections for what is logically one feature.
/// </summary>
public sealed class LlmProviderOptions
{
    public const string SectionName = "LlmProvider";

    /// <summary>
    /// Active provider. Must match exactly one of: "Gemini", "Claude", "OpenAI".
    /// Changed without code deployment via appsettings or environment variable
    /// <c>LlmProvider__Active</c>.
    /// </summary>
    public string Active { get; init; } = "Gemini";

    /// <summary>
    /// Session token budget. 0 = unlimited.
    /// When the session total reaches this value, further LLM calls are blocked
    /// and <c>GET /api/tokens</c> reports <c>isBudgetExceeded: true</c>.
    /// </summary>
    public int TokenBudget { get; init; }

    public LlmEndpointOptions Gemini { get; init; } = new()
    {
        Model = "gemini-2.5-flash"
    };

    public LlmEndpointOptions Claude { get; init; } = new()
    {
        Model = "claude-opus-4-8"
    };

    public LlmEndpointOptions OpenAI { get; init; } = new()
    {
        Model = "gpt-4o-mini"
    };

    /// <summary>
    /// Returns the API key and model for the currently <see cref="Active"/> provider.
    /// Unknown values fall back to Gemini's section; <see cref="Active"/> itself is
    /// validated when the <c>IChatClient</c> is constructed in Program.cs.
    /// </summary>
    public LlmEndpointOptions GetActiveEndpoint() =>
        Active.Equals("Claude", StringComparison.OrdinalIgnoreCase) ? Claude
        : Active.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) ? OpenAI
        : Gemini;
}

/// <summary>Per-provider API key and model name.</summary>
public sealed class LlmEndpointOptions
{
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Model identifier. Configurable so a new model can be adopted without
    /// a code change — just update appsettings and restart.
    /// </summary>
    public string Model { get; init; } = string.Empty;
}
