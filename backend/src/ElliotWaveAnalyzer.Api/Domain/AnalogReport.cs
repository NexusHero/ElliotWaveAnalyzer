namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic historical-analog read for a query setup: the ranked <see cref="Analogs"/> and
/// their aggregate <see cref="Stats"/>. This is fact — computed entirely by the engine, no LLM. An
/// optional natural-language <see cref="Narrative"/> may be attached afterwards (fact-guarded so it
/// cannot cite a number not present here); <see cref="NarrativeUnavailableReason"/> explains its
/// absence (no key configured, guard tripped, insufficient history) so the deterministic read always
/// stands on its own.
/// </summary>
/// <param name="AsOf">The query's as-of date; every analog concluded strictly before it (no leak).</param>
/// <param name="Analogs">The k nearest concluded analogs, most similar first.</param>
/// <param name="Stats">The aggregate measured resolution of those analogs.</param>
public sealed record AnalogReport(
    DateTimeOffset AsOf,
    IReadOnlyList<HistoricalAnalog> Analogs,
    AnalogStats Stats)
{
    /// <summary>Grounded natural-language summary of the analogs, or null if none was produced.</summary>
    public string? Narrative { get; init; }

    /// <summary>Why <see cref="Narrative"/> is absent (no LLM key, guard tripped, too few analogs).</summary>
    public string? NarrativeUnavailableReason { get; init; }
}
