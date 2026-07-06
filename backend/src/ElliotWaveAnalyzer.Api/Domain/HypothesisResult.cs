namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic verdict on one proposed structure hypothesis. The engine generated the structure
/// over the detected pivots and rule-checked it: a valid hypothesis carries its guideline
/// <see cref="Score"/>; a rejected one carries the specific <see cref="FailingRule"/> it violated (or
/// why it couldn't be formed). A rejected hypothesis is never presented as a valid count — the LLM
/// proposed it, but only the engine's rules can accept it (ADR-009).
/// </summary>
/// <param name="Structure">The proposed structure family (by name), e.g. "Zigzag".</param>
/// <param name="Reason">The LLM's one-line rationale for proposing it.</param>
/// <param name="IsValid">True only when the engine's hard rules accept the structure over the pivots.</param>
/// <param name="Score">Guideline score in [0,1] when valid and scorable; null otherwise.</param>
/// <param name="FailingRule">The hard rule that rejected it (or why it couldn't be formed); null when valid.</param>
public sealed record HypothesisResult(
    string Structure,
    string Reason,
    bool IsValid,
    double? Score,
    string? FailingRule);
