namespace ElliotWaveAnalyzer.Api.Application.Charting;

/// <summary>
/// A backend-agnostic RGBA colour used by the draw-op seam. Kept independent of SkiaSharp so the
/// scene the <see cref="AnnotatedChartComposer"/> produces stays in the Application layer and can be
/// asserted in tests without a rendering backend.
/// </summary>
/// <param name="R">Red channel, 0–255.</param>
/// <param name="G">Green channel, 0–255.</param>
/// <param name="B">Blue channel, 0–255.</param>
/// <param name="A">Alpha channel, 0–255 (255 = opaque).</param>
public sealed record ChartColor(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Returns this colour with its alpha replaced by <paramref name="alpha"/>.</summary>
    public ChartColor WithAlpha(byte alpha) => this with { A = alpha };
}
