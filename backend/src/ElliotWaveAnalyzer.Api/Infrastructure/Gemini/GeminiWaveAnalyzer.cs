using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Gemini;

/// <summary>
/// Deprecated: use <see cref="Llm.GeminiLlmProvider"/> registered as <see cref="ILlmWaveAnalyzer"/>.
/// This class is retained only so any code that injected <see cref="IGeminiWaveAnalyzer"/>
/// directly continues to compile until it is updated.
/// </summary>
[Obsolete("Inject ILlmWaveAnalyzer instead. This wrapper will be removed in a future version.")]
public sealed class GeminiWaveAnalyzer(Llm.GeminiLlmProvider inner) : IGeminiWaveAnalyzer
{
    public string ProviderName => inner.ProviderName;

    public Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default)
        => inner.ValidateAsync(symbol, candles, annotations, cancellationToken);
}
