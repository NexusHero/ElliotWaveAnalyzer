using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Common abstraction over all LLM providers for Elliott Wave validation.
///
/// WHY a shared interface instead of provider-specific ones:
/// ISP — callers depend only on wave validation, not on Gemini/Claude/OpenAI specifics.
/// OCP — new providers are added without touching existing code.
/// LSP — all implementations are interchangeable at the <c>LlmProvider:Active</c> config key.
///
/// Each implementation must:
///   1. Build a prompt via <see cref="Infrastructure.Llm.GeminiPromptBuilder"/> (provider-agnostic)
///   2. Call its LLM API and parse the JSON response
///   3. Return token usage so the <see cref="ITokenTracker"/> can accumulate the session total
/// </summary>
public interface ILlmWaveAnalyzer
{
    /// <summary>Provider identifier used for routing and reporting ("Gemini", "Claude", "OpenAI").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Validates the wave count against Elliott Wave rules via the provider's LLM.
    /// The returned <see cref="WaveValidationResult.TokenUsage"/> is always populated.
    /// </summary>
    Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default);
}
