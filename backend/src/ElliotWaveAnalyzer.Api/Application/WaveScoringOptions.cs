namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Tunable knobs for the wave grammar parser and its guideline scoring. Hard Elliott rules
/// are never weighted — they prune. These weights only rank the survivors, so changing them
/// reorders candidates but can never surface a rule-violating count.
/// </summary>
public sealed record WaveScoringOptions
{
    /// <summary>Weight of Fibonacci-proportion fit (distance to canonical ratios).</summary>
    public double FibWeight { get; init; } = 0.4;

    /// <summary>Weight of wave 2/4 alternation (impulses and diagonals only).</summary>
    public double AlternationWeight { get; init; } = 0.2;

    /// <summary>Weight of channel fit — linearity of the 1-3-5 terminals (impulses only).</summary>
    public double ChannelWeight { get; init; } = 0.2;

    /// <summary>Weight of time proportionality (no wave absurdly long vs. its siblings).</summary>
    public double TimeWeight { get; init; } = 0.2;

    /// <summary>
    /// How much of a node's final score comes from its children's quality (subdivided waves
    /// that themselves score well confirm the count's fractality); the rest is the node's own
    /// guideline fit. Terminal legs contribute a neutral 0.5.
    /// </summary>
    public double ChildScoreWeight { get; init; } = 0.3;

    /// <summary>Multiplier applied once per failed guideline (e.g. truncated zigzag C).</summary>
    public double GuidelinePenalty { get; init; } = 0.9;

    /// <summary>Full credit at a canonical ratio, fading to zero at this absolute distance.</summary>
    public double FibTolerance { get; init; } = 0.30;

    /// <summary>Trees kept per memo cell (structure kind × pivot interval).</summary>
    public int BeamWidth { get; init; } = 8;

    /// <summary>Final candidates returned, best score first.</summary>
    public int MaxCandidates { get; init; } = 6;

    /// <summary>
    /// Top-down: multiplier applied to a finer count whose structure class (motive vs. corrective)
    /// disagrees with the parent wave. A soft penalty — direction contradictions are hard-rejected,
    /// not weighted.
    /// </summary>
    public double TopDownClassMismatchPenalty { get; init; } = 0.6;

    /// <summary>
    /// Top-down: multiplier applied to a finer count whose price range spills outside the parent
    /// wave's price window (beyond <see cref="TopDownWindowTolerance"/>).
    /// </summary>
    public double TopDownOutOfWindowPenalty { get; init; } = 0.75;

    /// <summary>
    /// Top-down: fraction of the parent window's height a finer count may spill beyond either bound
    /// before it counts as out-of-window (absorbs pivot noise at the edges).
    /// </summary>
    public double TopDownWindowTolerance { get; init; } = 0.15;

    /// <summary>
    /// Parse at most this many trailing pivots. More pivots = quadratically more intervals;
    /// beyond ~60 the finest scale should be coarsened instead of parsed harder.
    /// </summary>
    public int MaxPivots { get; init; } = 60;

    /// <summary>Maximum legs a single wave may span before it must subdivide further.</summary>
    public int MaxWaveSpanLegs { get; init; } = 21;

    /// <summary>
    /// Hard cap on partition evaluations across the whole parse — the complexity guard that
    /// bounds wall-clock time on pathological input. When hit, the parse returns what it has
    /// and flags <see cref="Domain.WaveParseResult.SearchTruncated"/>.
    /// </summary>
    public long MaxEvaluations { get; init; } = 2_000_000;

    /// <summary>Shared default instance.</summary>
    public static WaveScoringOptions Default { get; } = new();
}
