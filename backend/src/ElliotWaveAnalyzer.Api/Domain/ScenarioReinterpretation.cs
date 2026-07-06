namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The concrete, re-projectable reading a count flips to if its primary invalidation breaks: the
/// same pivots re-read under the opposite structural mode (an impulse count's alternative reads
/// them as a correction, and vice-versa). Carrying the inputs — not an eagerly-computed
/// <see cref="WaveLevels"/> — keeps construction finite (the projection is resolved lazily via
/// <see cref="Application.ProjectionService.Resolve"/>, so an alternative-of-an-alternative never
/// recurses at build time) and keeps a single source of truth: only the projection engine produces
/// it. Pure data, no LLM.
/// </summary>
/// <param name="Structure">How to re-read the pivots (e.g. <see cref="StructureKind.Zigzag"/>). Ignored for a motive re-read, which is always positional.</param>
/// <param name="Motive">True → re-project as a motive count (<see cref="Application.ProjectionService.Project"/>); false → as a correction (<see cref="Application.ProjectionService.ProjectCorrective"/>).</param>
/// <param name="Annotations">The same pivots as the primary count, in date order.</param>
public sealed record ScenarioReinterpretation(
    StructureKind Structure,
    bool Motive,
    IReadOnlyList<WaveAnnotation> Annotations);
