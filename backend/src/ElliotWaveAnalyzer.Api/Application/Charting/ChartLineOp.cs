namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A straight line from (<paramref name="X1"/>, <paramref name="Y1"/>) to
/// (<paramref name="X2"/>, <paramref name="Y2"/>) in pixel space. Used for candle wicks, grid lines,
/// channel rays, the invalidation line and scenario arrows.
/// </summary>
/// <param name="X1">Start x.</param>
/// <param name="Y1">Start y.</param>
/// <param name="X2">End x.</param>
/// <param name="Y2">End y.</param>
/// <param name="Color">Stroke colour.</param>
/// <param name="StrokeWidth">Stroke width in pixels.</param>
/// <param name="Dashed">True to stroke a dashed line (e.g. alternate scenario arrows).</param>
public sealed record ChartLineOp(
    double X1,
    double Y1,
    double X2,
    double Y2,
    ChartColor Color,
    float StrokeWidth = 1f,
    bool Dashed = false) : ChartDrawOp;
