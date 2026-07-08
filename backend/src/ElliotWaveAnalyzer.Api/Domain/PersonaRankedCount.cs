namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One candidate in the persona-panel response: the same deterministic geometry
/// <see cref="RankedWaveCount"/> carries, plus which personas independently picked it as their
/// own top choice — the honest basis for tagging a saved analysis with a single persona
/// (<see cref="TrackAnalysisRequest.Persona"/>) later, since a merged consensus otherwise has no
/// one "author".
/// </summary>
public sealed record PersonaRankedCount(
    string Structure,
    WaveAnnotation Origin,
    IReadOnlyList<WaveAnnotation> Waves,
    WaveRuleReport RuleReport,
    WaveLevels? Levels,
    string Confidence,
    string Rationale,
    string Outlook,
    bool IsBest,
    IReadOnlyList<string> EndorsingPersonas)
{
    public WaveNode? Tree { get; init; }
    public decimal? Score { get; init; }
}
