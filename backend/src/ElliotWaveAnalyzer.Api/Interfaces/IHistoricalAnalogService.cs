using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Produces the historical-analog read for a symbol: fingerprints the current count, builds the
/// no-lookahead corpus of that symbol's past setups, retrieves the nearest, aggregates their measured
/// resolution and (when possible) attaches a fact-guarded narrative. Returns null when there is no
/// current rule-valid count or not enough history to compare against.
/// </summary>
public interface IHistoricalAnalogService
{
    /// <summary>Builds the analog report for <paramref name="symbol"/> at <paramref name="interval"/>.</summary>
    Task<AnalogReport?> AnalyzeAsync(
        string symbol,
        CandleInterval interval,
        CancellationToken cancellationToken = default);
}
