namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The result of an intelligent alternate-hypothesis pass (#186): the structures the LLM proposed
/// testing, each after the deterministic engine generated and rule-checked it. Validated hypotheses
/// join the analyst's option set (scored by the same guideline scorer as the beam search); rejected
/// ones are shown as "considered and rejected" with the failing rule, never as valid counts.
/// </summary>
/// <param name="Symbol">The instrument the hypotheses were tested on.</param>
/// <param name="Validated">Proposals the engine's hard rules accepted, best score first.</param>
/// <param name="Rejected">Proposals the engine rejected, with the rule that failed.</param>
/// <param name="ProposalCapHit">True when the LLM offered more proposals than the per-request cap allowed.</param>
/// <param name="Unavailable">Set when the feature is off (no LLM key); the deterministic search is unaffected.</param>
public sealed record AlternateHypothesesReport(
    string Symbol,
    IReadOnlyList<HypothesisResult> Validated,
    IReadOnlyList<HypothesisResult> Rejected,
    bool ProposalCapHit,
    string? Unavailable);
