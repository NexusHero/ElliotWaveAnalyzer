namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic weighted-aggregation result of a persona panel: the consensus ranking, how much
/// of the panel's weight agrees with it, and the weights that produced it. Produced only by
/// <see cref="Application.PersonaPanelAggregator"/> — the LLM personas rank and explain; the engine
/// owns the weighting and the merge.
/// </summary>
/// <param name="BestCandidateId">The candidate the panel's weighted vote favours.</param>
/// <param name="Rankings">Per-candidate merged assessment, most likely first.</param>
/// <param name="ConsensusScore">
/// Fraction of total persona weight that picked <see cref="BestCandidateId"/> as best, in [0, 1].
/// 1.0 is unanimous (by weight); lower values surface real disagreement rather than hiding it (AC4).
/// </param>
/// <param name="Weights">Each persona's weight, as used in this aggregation.</param>
public sealed record PersonaPanelConsensus(
    int BestCandidateId,
    IReadOnlyList<RankedCandidate> Rankings,
    double ConsensusScore,
    IReadOnlyList<PersonaWeight> Weights);
