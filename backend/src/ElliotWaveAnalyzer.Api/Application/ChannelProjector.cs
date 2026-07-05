using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Projects Elliott channels from a motive count's pivots — deterministic geometry, no LLM. The
/// base channel draws the 0→2 line with a parallel through wave 1; the acceleration channel draws
/// the 2→4 line with a parallel through wave 3 and projects the wave-5 target band one
/// acceleration-leg further in time. Lines are fitted in price space for a linear analysis and in
/// ln(price) space for a log-scaled one (so a straight channel on a log chart is a straight line
/// here too), with x measured in days from the origin pivot. Pure and deterministic.
/// </summary>
public static class ChannelProjector
{
    /// <summary>
    /// Channels for the count described by <paramref name="annotations"/> (origin first, then the
    /// wave terminals). Returns the base channel once wave 2 exists (≥3 pivots) and additionally the
    /// acceleration channel once wave 4 exists (≥5 pivots). Empty when there are too few pivots or a
    /// log fit is requested on non-positive prices.
    /// </summary>
    public static IReadOnlyList<Channel> Project(IReadOnlyList<WaveAnnotation> annotations, FibScale scale)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var p = annotations.OrderBy(a => a.Date).ToList();
        if (p.Count < 3)
        {
            return [];
        }

        if (scale == FibScale.Log && p.Any(a => a.Price <= 0m))
        {
            return [];
        }

        var origin = p[0].Date;
        var channels = new List<Channel>();

        // Base channel: 0→2 baseline, parallel through 1.
        var baseLine = LineThrough(p[0], p[2], scale, origin);
        channels.Add(new Channel(
            ChannelKind.Base, scale, origin,
            baseLine, ParallelThrough(p[1], baseLine.Slope, scale, origin),
            TargetLow: null, TargetHigh: null,
            Basis: "Base channel: 0→2 line, parallel through wave 1"));

        // Acceleration channel: 2→4 baseline, parallel through 3, projecting the wave-5 target.
        if (p.Count >= 5)
        {
            var accel = LineThrough(p[2], p[4], scale, origin);
            var parallel = ParallelThrough(p[3], accel.Slope, scale, origin);

            // Project one acceleration-leg (2→4 duration) beyond wave 4 — the usual wave-5 window.
            var x4 = Days(p[4], origin);
            var x2 = Days(p[2], origin);
            var projectAt = x4 + (x4 - x2);

            var a = ToPrice(accel.ValueAt(projectAt), scale);
            var b = ToPrice(parallel.ValueAt(projectAt), scale);

            channels.Add(new Channel(
                ChannelKind.Acceleration, scale, origin,
                accel, parallel,
                TargetLow: Math.Min(a, b), TargetHigh: Math.Max(a, b),
                Basis: "Acceleration channel: 2→4 line, parallel through wave 3; wave-5 target projected"));
        }

        return channels;
    }

    private static ChannelLine LineThrough(WaveAnnotation a, WaveAnnotation b, FibScale scale, DateTime origin)
    {
        var xa = Days(a, origin);
        var xb = Days(b, origin);
        var ya = ToY(a.Price, scale);
        var yb = ToY(b.Price, scale);
        var slope = xb == xa ? 0m : (yb - ya) / (xb - xa);
        return new ChannelLine(slope, ya - slope * xa);
    }

    private static ChannelLine ParallelThrough(WaveAnnotation point, decimal slope, FibScale scale, DateTime origin)
        => new(slope, ToY(point.Price, scale) - slope * Days(point, origin));

    private static decimal Days(WaveAnnotation a, DateTime origin) => (decimal)(a.Date - origin).TotalDays;

    private static decimal ToY(decimal price, FibScale scale)
        => scale == FibScale.Log ? (decimal)Math.Log((double)price) : price;

    private static decimal ToPrice(decimal y, FibScale scale)
        => scale == FibScale.Log ? (decimal)Math.Exp((double)y) : y;
}
