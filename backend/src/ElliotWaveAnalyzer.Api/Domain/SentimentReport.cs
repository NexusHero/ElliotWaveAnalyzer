namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic socionomics read for a count: the normalized mood <see cref="Series"/> and any
/// detected <see cref="Divergences"/>. This is fact — computed entirely by the engine, no LLM. An
/// optional natural-language <see cref="Narrative"/> may be attached afterwards (fact-guarded so it
/// cannot cite a mood value not present here); <see cref="NarrativeUnavailableReason"/> explains its
/// absence. <see cref="HasCoverage"/> is false when no sentiment provider covers the symbol — the
/// panel then shows an explicit "no sentiment coverage" state rather than a zero-filled series (AC4).
/// </summary>
/// <param name="HasCoverage">False when no provider has sentiment data for the symbol.</param>
/// <param name="Series">The normalized mood series, empty when <see cref="HasCoverage"/> is false.</param>
/// <param name="Divergences">Detected mood-vs-position divergences, most recent first.</param>
public sealed record SentimentReport(
    bool HasCoverage,
    IReadOnlyList<SentimentPoint> Series,
    IReadOnlyList<MoodDivergence> Divergences)
{
    /// <summary>Grounded natural-language summary of the mood picture, or null if none was produced.</summary>
    public string? Narrative { get; init; }

    /// <summary>Why <see cref="Narrative"/> is absent (no LLM key, guard tripped, no coverage).</summary>
    public string? NarrativeUnavailableReason { get; init; }

    /// <summary>The no-coverage report: an explicit, honest state — never a fabricated series (AC4).</summary>
    public static SentimentReport NoCoverage(string reason) =>
        new(false, [], [])
        {
            NarrativeUnavailableReason = reason,
        };
}
