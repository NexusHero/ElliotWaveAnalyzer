namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A top-down, multi-timeframe Elliott Wave read: the best count for each timeframe from coarsest
/// to finest, each constrained to live inside the wave unfolding on the timeframe above it, plus
/// the per-link consistency verdicts and a one-line prose chain. Fully deterministic — the LLM
/// never participates (this is the "the LLM never does geometry" invariant applied across scales).
/// </summary>
/// <param name="Timeframes">Per-timeframe counts, coarsest first.</param>
/// <param name="Links">Consistency verdict for each adjacent parent→child pair (one fewer than
/// <see cref="Timeframes"/>).</param>
/// <param name="Summary">Human-readable chain, e.g. "1W: Impulse (Correction (ABC) forming, down)
/// → 1D: Zigzag [Consistent] → 4H: Impulse [Tension]".</param>
public sealed record TopDownAnalysis(
    IReadOnlyList<TimeframeCount> Timeframes,
    IReadOnlyList<TimeframeConsistency> Links,
    string Summary);
