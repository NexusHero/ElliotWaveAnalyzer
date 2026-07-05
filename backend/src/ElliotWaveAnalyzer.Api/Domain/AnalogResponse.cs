namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The historical-analog read for a symbol as sent to the client: the aggregate measured
/// <see cref="Stats"/>, the ranked <see cref="Analogs"/>, and an optional grounded
/// <see cref="Narrative"/> (with a reason when it is absent). The deterministic figures always stand;
/// the narrative is only ever a summary of them.
/// </summary>
/// <param name="Symbol">The queried instrument.</param>
/// <param name="Timeframe">The timeframe the count was read on ("1D" / "1W").</param>
/// <param name="Stats">The aggregate resolution of the analogs.</param>
/// <param name="Analogs">The ranked analogs, most similar first.</param>
/// <param name="Narrative">A fact-guarded natural-language summary, or null.</param>
/// <param name="NarrativeUnavailableReason">Why the narrative is absent, when it is.</param>
public sealed record AnalogResponse(
    string Symbol,
    string Timeframe,
    AnalogStats Stats,
    IReadOnlyList<AnalogItem> Analogs,
    string? Narrative,
    string? NarrativeUnavailableReason)
{
    /// <summary>Maps the deterministic <see cref="AnalogReport"/> to the client shape.</summary>
    public static AnalogResponse From(string symbol, string timeframe, AnalogReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var analogs = report.Analogs
            .Select(a => new AnalogItem(
                a.Setup.Symbol,
                a.Setup.FormedAt,
                a.Setup.ConcludedAt,
                a.Setup.Outcome.ToString(),
                a.Setup.Features.Structure.ToString(),
                a.Setup.Features.Bullish,
                a.Similarity,
                a.Setup.ResolutionDays))
            .ToList();

        return new AnalogResponse(
            symbol, timeframe, report.Stats, analogs, report.Narrative, report.NarrativeUnavailableReason);
    }

    /// <summary>The response for a symbol with no current count or too little history to compare.</summary>
    public static AnalogResponse Insufficient(string symbol, string timeframe, string reason) =>
        new(symbol, timeframe, new AnalogStats(0, 0, 0, null, null, Sufficient: false), [], null, reason);
}
