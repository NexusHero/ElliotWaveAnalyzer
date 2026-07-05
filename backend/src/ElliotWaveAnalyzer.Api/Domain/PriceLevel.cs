namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A single horizontal price level (e.g. the hard invalidation line).
/// </summary>
/// <param name="Price">The price.</param>
/// <param name="Side">Where it sits relative to current price.</param>
/// <param name="Label">Short human label, e.g. "Invalidation (Wave 4 must hold Wave 1)".</param>
/// <param name="Basis">What it is derived from, e.g. "End of Wave 1".</param>
public sealed record PriceLevel(decimal Price, LevelSide Side, string Label, string Basis);
