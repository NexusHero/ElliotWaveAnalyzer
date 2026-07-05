namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One ranked interpretation in the API response: the deterministic geometry (origin +
/// labelled waves + rule report) merged with the LLM's qualitative read.
/// </summary>
public sealed record RankedWaveCount(
    string Structure,
    WaveAnnotation Origin,
    IReadOnlyList<WaveAnnotation> Waves,
    WaveRuleReport RuleReport,
    WaveLevels? Levels,
    string Confidence,
    string Rationale,
    string Outlook,
    bool IsBest)
{
    /// <summary>Nested parse tree behind this count (additive; null for legacy flat counts).</summary>
    public WaveNode? Tree { get; init; }

    /// <summary>Deterministic guideline score in [0, 1]; null for legacy counts.</summary>
    public decimal? Score { get; init; }
}
