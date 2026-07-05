namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A directional scenario arrow to draw from the latest candle toward a projected target. The primary
/// count is drawn solid, alternates dashed; the colour follows <see cref="Bullish"/>.
/// </summary>
/// <param name="Label">Short label shown at the arrow tip, e.g. "Primary" or "Alt 1".</param>
/// <param name="Bullish">True when the count points up (green), false down (red).</param>
/// <param name="Primary">True for the primary count (solid); false for an alternate (dashed).</param>
/// <param name="TargetPrice">The price the arrow points at.</param>
public sealed record ChartScenarioArrow(
    string Label,
    bool Bullish,
    bool Primary,
    decimal TargetPrice);
