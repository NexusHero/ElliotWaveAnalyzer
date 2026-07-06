namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The forward branches drawn from a count's currently-unfolding wave (#219): what happens next if
/// the wave holds its zone, and what the alternate reading resolves to if the invalidation breaks.
/// Computed once, non-recursively, from the primary <see cref="WaveLevels"/> — the projection engine
/// owns the geometry; the LLM is never in this path.
/// </summary>
/// <param name="InvalidationRetracePercent">
/// Where the hard invalidation sits as a percentage retracement of the last completed leg (e.g. an
/// invalidation "≈ 71% of Wave 3"). Null for extending waves, where no support-zone leg applies.
/// </param>
/// <param name="Speculative">
/// The one-step-ahead projection: the current wave hypothetically completes at the far edge of its
/// zone, and the next wave is projected from there. Flagged speculative in the UI; null when the
/// current wave has no zone edge to complete at.
/// </param>
/// <param name="Alternate">
/// The count that takes over if the invalidation breaks — the primary's
/// <see cref="ScenarioReinterpretation"/> resolved to real levels. Null when there is no alternative.
/// </param>
public sealed record ProjectionBranches(
    double? InvalidationRetracePercent,
    WaveLevels? Speculative,
    WaveLevels? Alternate);
