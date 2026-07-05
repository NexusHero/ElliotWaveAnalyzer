namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Forward-looking, deterministic price levels derived from a (partial or complete) Elliott
/// Wave count: the hard invalidation line (rule-based), the expected Fibonacci support zone
/// for the wave currently unfolding (guideline), forward target zones, and the alternative
/// count that applies if the invalidation breaks. Computed by
/// <see cref="Application.ProjectionService"/> — pure math, no LLM.
/// </summary>
/// <param name="UnfoldingWave">The wave assumed to be in progress, e.g. "Wave 4" or "Correction (ABC)".</param>
/// <param name="Bullish">True when the underlying impulse is up.</param>
/// <param name="Invalidation">The hard rule-based line that must hold (null if not determinable).</param>
/// <param name="SupportZone">Where the unfolding wave is expected to react (null if N/A).</param>
/// <param name="TargetZones">Forward projection zone(s) for where the move is likely heading.</param>
/// <param name="Alternative">The count that applies if <see cref="Invalidation"/> breaks.</param>
public sealed record WaveLevels(
    string UnfoldingWave,
    bool Bullish,
    PriceLevel? Invalidation,
    PriceZone? SupportZone,
    IReadOnlyList<PriceZone> TargetZones,
    AlternativeScenario? Alternative)
{
    /// <summary>
    /// The price scale the Fibonacci levels were computed in. Auto-selected from the count's price
    /// span (log once the span exceeds a few multiples of its low), always reported so the choice
    /// is explicit rather than hidden. See <see cref="Application.FibMath.AutoSelect"/>.
    /// </summary>
    public FibScale Scale { get; init; } = FibScale.Linear;

    /// <summary>
    /// Scored Fibonacci confluence zones — the "green boxes" where several ratios (and degrees)
    /// cluster — for the wave currently unfolding, strongest first. Computed log-correctly when
    /// <see cref="Scale"/> is <see cref="FibScale.Log"/>. Empty when no legs are available.
    /// See <see cref="Application.FibConfluenceCalculator"/>.
    /// </summary>
    public IReadOnlyList<ConfluenceZone> ConfluenceZones { get; init; } = [];
}
