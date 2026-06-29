namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Which side of the current price a level sits on.</summary>
public enum LevelSide
{
    /// <summary>The level is below current price (support / floor in a bullish count).</summary>
    Below,

    /// <summary>The level is above current price (resistance / cap in a bearish count).</summary>
    Above,
}

/// <summary>
/// A single horizontal price level (e.g. the hard invalidation line).
/// </summary>
/// <param name="Price">The price.</param>
/// <param name="Side">Where it sits relative to current price.</param>
/// <param name="Label">Short human label, e.g. "Invalidation (Wave 4 must hold Wave 1)".</param>
/// <param name="Basis">What it is derived from, e.g. "End of Wave 1".</param>
public sealed record PriceLevel(decimal Price, LevelSide Side, string Label, string Basis);

/// <summary>A price band (e.g. the expected Fibonacci retracement zone for the unfolding wave).</summary>
/// <param name="Low">Lower bound.</param>
/// <param name="High">Upper bound.</param>
/// <param name="Label">Short human label, e.g. "Wave 4 support (23.6–38.2% of Wave 3)".</param>
/// <param name="Basis">Derivation, e.g. "Fibonacci retracement of Wave 3".</param>
public sealed record PriceZone(decimal Low, decimal High, string Label, string Basis);

/// <summary>The count that would apply if the primary count's invalidation breaks.</summary>
/// <param name="Name">Short name, e.g. "Ending diagonal / ABC".</param>
/// <param name="Note">One-line explanation of when and why it takes over.</param>
public sealed record AlternativeScenario(string Name, string Note);

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
    AlternativeScenario? Alternative);
