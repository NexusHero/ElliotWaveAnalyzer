using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Turns a set of retrieved analogs into the aggregate, measured resolution shown to the analyst —
/// hit-rate, the target/invalidated split, and the median time to resolution. Every figure is
/// computed <em>only</em> from concluded analogs (pending rows never enter the denominator), and the
/// result is flagged <see cref="AnalogStats.Sufficient"/> only when enough analogs back it, so a
/// one- or two-sample rate is never presented as reliable.
/// </summary>
public static class AnalogAggregator
{
    /// <summary>Fewer concluded analogs than this and the rates are not to be trusted.</summary>
    public const int MinimumSample = 5;

    /// <summary>Aggregates the analogs' recorded outcomes into grounded statistics.</summary>
    public static AnalogStats Aggregate(
        IReadOnlyList<HistoricalAnalog> analogs,
        int minimumSample = MinimumSample)
    {
        ArgumentNullException.ThrowIfNull(analogs);

        // Defensive: the retriever already excludes pending setups, but the denominator must never
        // silently include one, so re-filter here — the contract is "concluded only".
        var concluded = analogs.Where(a => a.Setup.Concluded).ToList();
        var sampleCount = concluded.Count;
        var targetReached = concluded.Count(a => a.Setup.Outcome == AnalysisOutcome.TargetReached);
        var invalidated = concluded.Count(a => a.Setup.Outcome == AnalysisOutcome.Invalidated);

        double? hitRate = sampleCount == 0 ? null : (double)targetReached / sampleCount;
        double? medianDays = Median(
            concluded.Select(a => a.Setup.ResolutionDays).OfType<double>().ToList());

        return new AnalogStats(
            sampleCount,
            targetReached,
            invalidated,
            hitRate,
            medianDays,
            sampleCount >= minimumSample);
    }

    private static double? Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return null;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }
}
