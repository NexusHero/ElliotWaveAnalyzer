using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Produces the socionomics read for a symbol's current count: fetches sentiment coverage, normalizes
/// it, detects mood-vs-wave-position divergences against the supplied pivots, and (when possible)
/// attaches a fact-guarded narrative. Never mutates or reads the pivots' geometry beyond their date and
/// price — the count itself is unaffected by this pass.
/// </summary>
public interface ISentimentAnalysisService
{
    /// <summary>
    /// Builds the sentiment report for <paramref name="symbol"/> against <paramref name="pivots"/>
    /// (the analyst's own or the auto-ranked count's pivots), looking back <paramref name="days"/>.
    /// </summary>
    Task<SentimentReport> AnalyzeAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> pivots,
        int days,
        CancellationToken cancellationToken = default);
}
