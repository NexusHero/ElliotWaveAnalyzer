using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Derives forward-looking price levels from an Elliott Wave count: the hard invalidation
/// line for the wave currently unfolding, its expected Fibonacci support zone, forward target
/// zones, and the alternative count if the invalidation breaks.
///
/// CONVENTION (matches <see cref="ElliottRuleChecker"/>): annotations sorted by date are read
/// as pivots P0, P1, … where the move P[i-1] → P[i] is "wave i", P0 being the origin. A count
/// with k pivots after the origin therefore has waves 1..k complete and wave k+1 unfolding.
/// A complete five-wave impulse (k = 5) is followed by a corrective phase.
///
/// Pure (static, no I/O) so it is fully unit-testable; works for both bullish and bearish
/// impulses by carrying the sign of each wave's leg.
/// </summary>
public static class ProjectionService
{
    /// <summary>
    /// Projects levels for the given count. Returns null when there are too few pivots to
    /// determine direction (fewer than 2).
    /// </summary>
    public static WaveLevels? Project(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        if (p.Count < 2)
        {
            return null;
        }

        var bullish = p[1] > p[0];
        var completedWaves = p.Count - 1; // P0 is the origin
        var unfolding = completedWaves + 1;

        return unfolding switch
        {
            2 => Wave2(p, bullish),
            3 => Wave3(p, bullish),
            4 => Wave4(p, bullish),
            5 => Wave5(p, bullish),
            _ => CompleteImpulseCorrection(p, bullish), // unfolding >= 6 (impulse complete)
        };
    }

    // Wave 2 unfolding: retraces wave 1, must not pass the origin.
    private static WaveLevels Wave2(IReadOnlyList<decimal> p, bool bull)
    {
        var leg1 = p[1] - p[0];
        var support = Zone(
            Retrace(p[1], leg1, 0.5m), Retrace(p[1], leg1, 0.618m),
            "Wave 2 support (50–61.8% of Wave 1)", "Fibonacci retracement of Wave 1");

        return new WaveLevels(
            "Wave 2", bull,
            new PriceLevel(p[0], Side(bull), "Invalidation — Wave 2 must hold the origin", "Origin (start of Wave 1)"),
            support,
            [],
            new AlternativeScenario("Corrective rise",
                "If price breaks the origin, the advance was likely corrective, not the start of an impulse."));
    }

    // Wave 3 unfolding: extends from the end of wave 2; must not re-enter below wave 2's start.
    private static WaveLevels Wave3(IReadOnlyList<decimal> p, bool bull)
    {
        var leg1 = p[1] - p[0];
        var target = Zone(
            Project(p[2], leg1, 1.0m), Project(p[2], leg1, 1.618m),
            "Wave 3 target (1.0–1.618× Wave 1)", "Fibonacci extension of Wave 1");

        return new WaveLevels(
            "Wave 3", bull,
            new PriceLevel(p[2], Side(bull), "Invalidation — Wave 3 must hold Wave 2's start", "End of Wave 2"),
            null,
            [target],
            new AlternativeScenario("Corrective B-wave",
                "If price reverses below Wave 2, the move up may be a corrective B-wave rather than Wave 3."));
    }

    // Wave 4 unfolding: pulls back from the wave 3 high; must not overlap wave 1 (the classic line).
    private static WaveLevels Wave4(IReadOnlyList<decimal> p, bool bull)
    {
        var leg3 = p[3] - p[2];
        var support = Zone(
            Retrace(p[3], leg3, 0.236m), Retrace(p[3], leg3, 0.382m),
            "Wave 4 support (23.6–38.2% of Wave 3)", "Fibonacci retracement of Wave 3");

        return new WaveLevels(
            "Wave 4", bull,
            new PriceLevel(p[1], Side(bull), "Invalidation — Wave 4 must not overlap Wave 1", "End of Wave 1"),
            support,
            [],
            new AlternativeScenario("Ending diagonal / ABC",
                "If price breaks the Wave 1 level, the structure may be an ending diagonal (overlap allowed) "
                + "or the prior 1-2-3 was a corrective A-B-C."));
    }

    // Wave 5 unfolding: final push from the wave 4 low; target often equals wave 1.
    private static WaveLevels Wave5(IReadOnlyList<decimal> p, bool bull)
    {
        var leg1 = p[1] - p[0];
        var net = p[3] - p[0]; // waves 1-3 net travel
        var target = Zone(
            Project(p[4], leg1, 1.0m), Project(p[4], net, 0.618m),
            "Wave 5 target (≈ Wave 1, or 0.618× of Waves 1–3)", "Fibonacci projection");

        return new WaveLevels(
            "Wave 5", bull,
            new PriceLevel(p[4], Side(bull), "Invalidation — Wave 5 must hold the Wave 4 low", "End of Wave 4"),
            null,
            [target],
            new AlternativeScenario("Wave 4 not complete",
                "If price breaks the Wave 4 low, Wave 4 may still be extending rather than Wave 5 being underway."));
    }

    // Complete five-wave impulse: a corrective phase (ABC) is expected to follow.
    private static WaveLevels CompleteImpulseCorrection(IReadOnlyList<decimal> p, bool bull)
    {
        var impulse = p[5] - p[0];
        // Correction retraces the whole impulse; commonly into the Wave 4 area (p[4]).
        var target = Zone(
            Retrace(p[5], impulse, 0.382m), Retrace(p[5], impulse, 0.618m),
            "Correction target (38.2–61.8% of the impulse; Wave 4 area)", "Fibonacci retracement of Waves 1–5");

        // For a bullish impulse the correction is down, so a break ABOVE the Wave 5 high
        // invalidates "impulse complete"; mirror for bearish.
        return new WaveLevels(
            "Correction (ABC)", bull,
            new PriceLevel(p[5], Side(!bull), "Invalidation — a break beyond the Wave 5 high reopens the impulse", "End of Wave 5"),
            null,
            [target],
            new AlternativeScenario("Wave 5 extension",
                "If price exceeds the Wave 5 high, Wave 5 may be extending rather than the correction having begun."));
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static LevelSide Side(bool supportBelow) => supportBelow ? LevelSide.Below : LevelSide.Above;

    /// <summary>Retraces a signed leg back from its end by fraction <paramref name="f"/>.</summary>
    private static decimal Retrace(decimal legEnd, decimal leg, decimal f) => legEnd - f * leg;

    /// <summary>Projects from <paramref name="basePrice"/> by <paramref name="m"/>× the leg, in the leg's direction.</summary>
    private static decimal Project(decimal basePrice, decimal leg, decimal m)
        => basePrice + m * Math.Abs(leg) * Math.Sign(leg);

    private static PriceZone Zone(decimal a, decimal b, string label, string basis)
        => new(Math.Min(a, b), Math.Max(a, b), label, basis);
}
