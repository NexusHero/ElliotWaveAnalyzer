using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Validates an Elliott Wave count via an LLM. A single implementation
/// (<c>LlmWaveAnalyzer</c>) targets the Microsoft.Extensions.AI <c>IChatClient</c>
/// abstraction; the concrete provider (Gemini/Claude/OpenAI) is chosen at the
/// composition root.
///
/// WHY this interface still exists with one implementation:
/// it keeps the application layer (<see cref="IWaveAnalysisService"/>) depending on an
/// abstraction it can mock in tests (DIP), and leaves room for alternative strategies
/// (e.g. an ensemble of providers, or a deterministic rules-only analyzer) without
/// touching callers.
/// </summary>
public interface ILlmWaveAnalyzer
{
    /// <summary>Provider identifier used for reporting ("Gemini", "Claude", "OpenAI").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Validates the wave count against Elliott Wave rules via the LLM and returns the
    /// assessment together with the token usage of the call.
    /// </summary>
    Task<LlmValidation> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default);
}
