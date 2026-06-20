using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Abstraction over the Gemini API for Elliott Wave validation.
///
/// WHY a dedicated interface instead of a generic "IAiService":
/// ISP — callers only depend on wave validation, not on general AI capabilities.
/// Makes tests trivial: mock this interface, don't stub HTTP calls to Google.
///
/// The concrete implementation (<see cref="Infrastructure.Gemini.GeminiWaveAnalyzer"/>)
/// is the only class that knows about Google.GenAI. Nothing else does.
/// </summary>
public interface IGeminiWaveAnalyzer
{
    /// <summary>
    /// Sends the wave count and candle context to Gemini and returns a structured validation.
    /// </summary>
    /// <param name="symbol">Ticker symbol for context (e.g. "BTC").</param>
    /// <param name="candles">
    /// The candles covering the annotated period. Used to provide price context
    /// (range, trend) to Gemini — NOT sent as an image, but summarized as text.
    /// </param>
    /// <param name="annotations">
    /// User-placed wave labels in chronological order.
    /// Must contain at least 2 annotations.
    /// </param>
    Task<WaveValidationResult> ValidateAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveAnnotation> annotations,
        CancellationToken cancellationToken = default);
}
