using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Composes the socionomics read: picks the first <see cref="ISentimentProvider"/> that supports the
/// symbol, normalizes its readings, detects mood-vs-wave-position divergences against the supplied
/// pivots, and hands the deterministic report to <see cref="ISentimentNarrator"/> for an optional
/// summary. With no provider covering the symbol, or an empty reading set, returns
/// <see cref="SentimentReport.NoCoverage"/> — never a fabricated series (AC4).
/// </summary>
public sealed class SentimentAnalysisService(
    IEnumerable<ISentimentProvider> providers,
    ISentimentNarrator narrator) : ISentimentAnalysisService
{
    /// <inheritdoc/>
    public async Task<SentimentReport> AnalyzeAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> pivots,
        int days,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(pivots);

        var provider = providers.FirstOrDefault(p => p.Supports(symbol));
        if (provider is null)
        {
            return SentimentReport.NoCoverage("No sentiment provider is configured for this symbol.");
        }

        var raw = await provider.GetSentimentAsync(symbol, days, cancellationToken);
        if (raw.Count == 0)
        {
            return SentimentReport.NoCoverage("The sentiment provider returned no coverage for this symbol.");
        }

        var series = SentimentIndexBuilder.Normalize(raw);
        var divergences = MoodDivergenceDetector.Detect(pivots, series);
        var report = new SentimentReport(true, series, divergences);

        return await narrator.NarrateAsync(report, cancellationToken);
    }
}
