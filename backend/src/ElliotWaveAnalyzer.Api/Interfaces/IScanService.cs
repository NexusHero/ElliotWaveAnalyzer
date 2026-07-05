using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Scans a set of symbols for Elliott Wave setups: runs the deterministic pipeline over each and
/// returns the matching hits, ranked by relevance. No LLM — cheap enough to sweep the universe.
/// </summary>
public interface IScanService
{
    /// <summary>
    /// Scans <paramref name="symbols"/> (or the configured default universe when null/empty) on
    /// <paramref name="timeframe"/>, keeps hits passing <paramref name="filter"/>, and returns the top
    /// <paramref name="limit"/> ranked most-relevant first. One failing symbol never aborts the scan.
    /// </summary>
    Task<ScanResult> ScanAsync(
        IReadOnlyList<string>? symbols,
        ScanFilter filter,
        string timeframe,
        int limit,
        CancellationToken cancellationToken = default);
}
