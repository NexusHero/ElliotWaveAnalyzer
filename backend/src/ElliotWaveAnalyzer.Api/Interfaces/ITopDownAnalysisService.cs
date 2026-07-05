using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Runs a deterministic top-down, multi-timeframe Elliott Wave analysis for a symbol: fetches
/// candles for each timeframe (coarsest → finest), detects swing pivots, and constrains each
/// finer count to the wave unfolding on the timeframe above it. No LLM is involved.
/// </summary>
public interface ITopDownAnalysisService
{
    /// <summary>
    /// Analyzes <paramref name="symbol"/> across the configured timeframe ladder.
    /// </summary>
    /// <param name="symbol">Market symbol (e.g. "AAPL").</param>
    /// <param name="thresholdPercent">ZigZag reversal sensitivity for pivot detection.</param>
    /// <returns>The per-timeframe counts and consistency verdicts, coarsest first.</returns>
    Task<TopDownAnalysis> AnalyzeAsync(
        string symbol,
        decimal thresholdPercent,
        CancellationToken cancellationToken = default);
}
