using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Composes the deterministic historical-analog read: retrieve the k nearest concluded,
/// no-lookahead analogs of a query setup, then aggregate their measured resolution. The result is
/// pure fact — no LLM is involved here; a fact-guarded narrative may be attached afterwards. This is
/// the seam the endpoint calls: give it a query fingerprint and a corpus, get a grounded report back.
/// </summary>
public static class HistoricalAnalogReporter
{
    /// <summary>Default neighbourhood size — enough analogs to make a rate meaningful, few enough to show.</summary>
    public const int DefaultK = 25;

    /// <summary>Builds the deterministic analog report (no narrative) for a query as of a date.</summary>
    public static AnalogReport Report(
        SetupFeatures query,
        IEnumerable<HistoricalSetup> corpus,
        DateTimeOffset asOf,
        int k = DefaultK,
        int minimumSample = AnalogAggregator.MinimumSample)
    {
        var analogs = AnalogRetriever.Retrieve(query, corpus, asOf, k);
        var stats = AnalogAggregator.Aggregate(analogs, minimumSample);
        return new AnalogReport(asOf, analogs, stats);
    }
}
