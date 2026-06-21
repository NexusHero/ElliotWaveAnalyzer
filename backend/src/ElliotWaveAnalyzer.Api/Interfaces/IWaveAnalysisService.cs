using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Orchestrates Elliott Wave validation: fetches candle context for the annotated
/// period, then delegates to <see cref="ILlmWaveAnalyzer"/> for the actual assessment.
/// </summary>
public interface IWaveAnalysisService
{
    /// <summary>
    /// Validates the given wave annotations against Elliott Wave rules via the
    /// configured LLM provider.
    /// </summary>
    /// <param name="symbol">Market symbol (e.g. "BTC").</param>
    /// <param name="annotations">
    /// Wave labels placed by the user, at least 2, in chronological order.
    /// </param>
    /// <exception cref="ArgumentException">
    /// When annotations are empty, contain invalid labels, or are not in chronological order.
    /// </exception>
    Task<LlmValidation> ValidateAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default);
}
