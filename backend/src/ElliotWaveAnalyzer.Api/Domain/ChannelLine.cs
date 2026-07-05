namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A straight channel line as <c>y = Slope·x + Intercept</c>, where <c>x</c> is days since the
/// channel's origin pivot and <c>y</c> is price (linear scale) or ln(price) (log scale). Two
/// parallel lines (a baseline and its parallel) make a channel; keeping the equation explicit lets
/// the renderer draw the ray across the whole chart and lets tests assert the geometry numerically.
/// </summary>
/// <param name="Slope">Change in y per day.</param>
/// <param name="Intercept">y at x = 0 (the origin pivot's date).</param>
public sealed record ChannelLine(decimal Slope, decimal Intercept)
{
    /// <summary>Evaluates the line at <paramref name="days"/> since the origin, in the line's y-space.</summary>
    public decimal ValueAt(decimal days) => Slope * days + Intercept;
}
