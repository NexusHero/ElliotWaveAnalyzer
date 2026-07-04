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

/// <summary>
/// The LLM's pure ranking of the candidates (ids + prose only — no geometry). Paired with
/// <see cref="TokenUsage"/> in <see cref="AutoWaveAnalysis"/>.
/// </summary>
/// <param name="BestCandidateId">Id of the most likely candidate.</param>
/// <param name="MarketSummary">One-paragraph read of the overall market structure.</param>
/// <param name="Rankings">Per-candidate assessment, most likely first.</param>
public sealed record AutoWaveRanking(
    int BestCandidateId,
    string MarketSummary,
    IReadOnlyList<RankedCandidate> Rankings);

/// <summary>The LLM's assessment of a single candidate, keyed by <see cref="CandidateId"/>.</summary>
/// <param name="CandidateId">Matches <see cref="WaveCandidate.Id"/>.</param>
/// <param name="Confidence">"high" | "medium" | "low".</param>
/// <param name="Rationale">Why this count fits (or doesn't).</param>
/// <param name="Outlook">What the count implies for the likely next move, per Elliott theory.</param>
public sealed record RankedCandidate(
    int CandidateId,
    string Confidence,
    string Rationale,
    string Outlook);

/// <summary>Pairs the pure <see cref="AutoWaveRanking"/> with the token cost of the LLM call.</summary>
public sealed record AutoWaveAnalysis(AutoWaveRanking Ranking, TokenUsage Usage);

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

/// <summary>
/// Full response of <c>POST /api/wave-analysis/auto</c>: every candidate count the system
/// found and ranked, an overall market summary, and the token usage of the LLM call.
/// <see cref="Rankings"/> is empty when no rule-valid wave structure could be detected.
/// </summary>
public sealed record AutoWaveAnalysisResponse(
    IReadOnlyList<RankedWaveCount> Rankings,
    string MarketSummary,
    TokenUsage Usage)
{
    /// <summary>
    /// True when the parser's evaluation budget truncated the search — the rankings are
    /// valid but coverage was bounded (never silently dropped).
    /// </summary>
    public bool SearchTruncated { get; init; }
}
