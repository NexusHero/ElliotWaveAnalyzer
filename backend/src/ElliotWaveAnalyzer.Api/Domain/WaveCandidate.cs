namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A machine-generated candidate Elliott Wave count. The geometry (origin + labelled wave
/// terminals) is produced deterministically from detected swing pivots and is pre-validated
/// against the canonical rules in <see cref="RuleReport"/>. The LLM only ranks and explains
/// these candidates — it never invents prices or pivots, which is what keeps the "full-auto"
/// mode trustworthy (the hybrid principle: deterministic geometry, LLM judgement on top).
/// </summary>
/// <param name="Id">Stable index within a single analysis run; used to map the LLM's ranking back.</param>
/// <param name="Structure">Pattern kind, e.g. "Impulse".</param>
/// <param name="Origin">
/// The starting pivot of the structure (where wave 1 begins). Carried separately from
/// <see cref="Waves"/> because it has no Elliott number of its own — consumers should render
/// it as a start marker, not a labelled wave. Its <see cref="WaveAnnotation.Label"/> is a
/// placeholder only.
/// </param>
/// <param name="Waves">The labelled wave terminals: "1".."5" for an impulse.</param>
/// <param name="RuleReport">Deterministic rule + Fibonacci report for origin + waves.</param>
/// <param name="Levels">Deterministic forward levels (invalidation, support/target zones); null if undeterminable.</param>
public sealed record WaveCandidate(
    int Id,
    string Structure,
    WaveAnnotation Origin,
    IReadOnlyList<WaveAnnotation> Waves,
    WaveRuleReport RuleReport,
    WaveLevels? Levels)
{
    /// <summary>
    /// Nested parse tree behind this count (additive — null for candidates from the legacy
    /// flat generator). The top-level geometry in <see cref="Waves"/> is the tree's first
    /// level; the tree adds sub-wave structure, degrees and per-node rule reports.
    /// </summary>
    public WaveNode? Tree { get; init; }

    /// <summary>Deterministic guideline score in [0, 1]; null for legacy candidates.</summary>
    public decimal? Score { get; init; }
}
