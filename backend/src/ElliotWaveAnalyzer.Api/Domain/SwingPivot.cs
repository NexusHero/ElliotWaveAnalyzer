namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A turning point in the price series detected deterministically (ZigZag) from candles.
/// Pivots alternate between highs and lows and are the raw material the
/// <see cref="WaveAnnotation"/> candidate counts are built from.
/// </summary>
/// <param name="Date">UTC time of the candle at the swing extreme.</param>
/// <param name="Price">Price at the swing extreme — the candle High for a swing high, the
/// candle Low for a swing low (wick-aware, not the close).</param>
/// <param name="IsHigh">True for a swing high, false for a swing low.</param>
public sealed record SwingPivot(DateTime Date, decimal Price, bool IsHigh);
