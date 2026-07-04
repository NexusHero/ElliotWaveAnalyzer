using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Orchestrates market data retrieval and indicator calculation for a given symbol.
/// Hides provider selection and calculation details from the API layer.
/// </summary>
public interface ITechnicalAnalysisService
{
    /// <summary>
    /// Returns candles + MACD + RSI for <paramref name="symbol"/> over the last <paramref name="days"/>
    /// days, at the requested <paramref name="interval"/> (daily fetched, then resampled). Indicators
    /// are computed on the resampled series.
    /// </summary>
    /// <exception cref="ArgumentException">When no provider supports the requested symbol.</exception>
    Task<TechnicalAnalysisResult> GetAnalysisAsync(
        string symbol,
        int days = 90,
        CandleInterval interval = CandleInterval.OneDay,
        CancellationToken cancellationToken = default);
}
