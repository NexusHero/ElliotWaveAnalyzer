using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Finds the <c>k</c> historical setups most similar to a query, by cosine over the deterministic
/// feature vectors. Two hard constraints keep the result honest:
/// <list type="bullet">
///   <item><b>Concluded only</b> — a setup that never settled is not evidence; pending rows are dropped.</item>
///   <item><b>No lookahead</b> — only setups that concluded <em>strictly before</em> the query's as-of
///   date are eligible, so the panel can be shown for any historical moment (and inside backtests)
///   without leaking outcomes that had not happened yet.</item>
/// </list>
/// Ties on similarity are broken deterministically (oldest formation, then symbol) so the same query
/// against the same corpus always yields the identical ordered set.
/// </summary>
public static class AnalogRetriever
{
    /// <summary>Returns the k nearest concluded analogs that concluded before <paramref name="asOf"/>.</summary>
    public static IReadOnlyList<HistoricalAnalog> Retrieve(
        SetupFeatures query,
        IEnumerable<HistoricalSetup> corpus,
        DateTimeOffset asOf,
        int k)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        if (k <= 0) return [];

        var queryVector = SetupFeatureVector.Encode(query);

        return corpus
            .Where(setup => setup.Concluded && setup.ConcludedAt!.Value < asOf)
            .Select(setup => new HistoricalAnalog(
                setup,
                SetupFeatureVector.Cosine(queryVector, SetupFeatureVector.Encode(setup.Features))))
            .OrderByDescending(analog => analog.Similarity)
            .ThenBy(analog => analog.Setup.FormedAt)
            .ThenBy(analog => analog.Setup.Symbol, StringComparer.Ordinal)
            .Take(k)
            .ToList();
    }
}
