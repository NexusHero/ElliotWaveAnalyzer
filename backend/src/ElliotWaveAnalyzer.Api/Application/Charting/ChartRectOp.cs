namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// An axis-aligned rectangle in pixel space with an optional fill and optional stroke. Used for
/// candle bodies, shaded confluence/entry/target zone boxes and pane framing.
/// </summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Fill">Fill colour, or null for no fill.</param>
/// <param name="Stroke">Stroke colour, or null for no border.</param>
/// <param name="StrokeWidth">Border width in pixels (ignored when <paramref name="Stroke"/> is null).</param>
public sealed record ChartRectOp(
    double X,
    double Y,
    double Width,
    double Height,
    ChartColor? Fill,
    ChartColor? Stroke = null,
    float StrokeWidth = 1f) : ChartDrawOp;
