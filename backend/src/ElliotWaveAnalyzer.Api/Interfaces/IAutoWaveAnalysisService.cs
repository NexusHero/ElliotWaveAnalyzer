using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Orchestrates the full-auto ("magic button") wave analysis: fetches candles, detects swing
/// pivots, generates rule-valid candidate counts, and has the LLM rank and explain them.
/// </summary>
public interface IAutoWaveAnalysisService
{
    /// <summary>
    /// Runs the end-to-end auto analysis for <paramref name="symbol"/>.
    /// </summary>
    /// <param name="symbol">Market symbol (e.g. "BTC").</param>
    /// <param name="lookbackDays">How many days of history to analyse.</param>
    /// <param name="thresholdPercent">ZigZag reversal sensitivity in percent.</param>
    /// <returns>
    /// Ranked candidate counts with a market summary. <see cref="AutoWaveAnalysisResponse.Rankings"/>
    /// is empty (and no LLM call is made) when no rule-valid structure is detected.
    /// </returns>
    Task<AutoWaveAnalysisResponse> AnalyzeAsync(
        string symbol,
        int lookbackDays,
        decimal thresholdPercent,
        CancellationToken cancellationToken = default);
}
