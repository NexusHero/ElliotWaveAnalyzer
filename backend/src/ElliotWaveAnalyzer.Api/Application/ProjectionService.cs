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

        var levels = unfolding switch
        {
            2 => Wave2(p, bullish),
            3 => Wave3(p, bullish),
            4 => Wave4(p, bullish),
            5 => Wave5(p, bullish),
            _ => CompleteImpulseCorrection(p, bullish), // unfolding >= 6 (impulse complete)
        };

        var scale = FibMath.AutoSelect(p);
        return levels with
        {
            Scale = scale,
            ConfluenceZones = MotiveConfluence(p, unfolding, scale),
            Channels = ChannelProjector.Project(annotations, scale),
        };
    }

    // Log-correct confluence zones for the wave currently unfolding, built from the legs that
    // matter for that wave. Wave 5 draws on two legs (Wave 1 and net Waves 1–3) so its target
    // box is a genuine multi-ratio cluster; the others project a single leg.
    private static IReadOnlyList<ConfluenceZone> MotiveConfluence(
        IReadOnlyList<decimal> p, int unfolding, FibScale scale) => unfolding switch
    {
        2 => FibConfluenceCalculator.EntryZones([new FibLeg(p[0], p[1], "Wave 1", 1.0m)], scale),
        3 => FibConfluenceCalculator.TargetZones([new FibLeg(p[0], p[1], "Wave 1", 1.0m)], p[2], scale),
        4 => FibConfluenceCalculator.EntryZones([new FibLeg(p[2], p[3], "Wave 3", 1.0m)], scale),
        5 => FibConfluenceCalculator.TargetZones(
            [new FibLeg(p[0], p[1], "Wave 1", 1.0m), new FibLeg(p[0], p[3], "Waves 1–3", 1.5m)], p[4], scale),
        _ => FibConfluenceCalculator.EntryZones([new FibLeg(p[0], p[5], "Waves 1–5", 1.0m)], scale),
    };

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

    // ─── corrective structures ────────────────────────────────────────────────

    /// <summary>
    /// Projects levels for a corrective count (zigzag, flat or triangle). Same positional
    /// convention: P0 is the origin, each further pivot completes one leg, leg k+1 is
    /// unfolding. Returns null with fewer than 2 points or for non-corrective kinds
    /// (motive counts go through <see cref="Project"/>).
    /// </summary>
    public static WaveLevels? ProjectCorrective(
        IReadOnlyList<WaveAnnotation> annotations, StructureKind kind)
    {
        ArgumentNullException.ThrowIfNull(annotations);

        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        if (p.Count < 2)
        {
            return null;
        }

        var bullish = p[1] > p[0];
        var unfolding = p.Count; // origin + k completed legs → leg k+1 unfolding

        var levels = kind switch
        {
            StructureKind.Zigzag or StructureKind.Flat => unfolding switch
            {
                2 => CorrectionWaveB(p, bullish, kind),
                3 => CorrectionWaveC(p, bullish),
                _ => CorrectionComplete(p, bullish),
            },
            StructureKind.Triangle => unfolding <= 5
                ? TriangleLeg(p, bullish, unfolding)
                : TriangleThrust(p, bullish),
            _ => null,
        };

        if (levels is null)
        {
            return null;
        }

        var scale = FibMath.AutoSelect(p);
        return levels with { Scale = scale, ConfluenceZones = CorrectiveConfluence(p, kind, unfolding, scale) };
    }

    // Confluence zones for a corrective leg. Wave B/C project Wave A; a completed ABC retraces the
    // whole correction; triangle legs retrace the prior leg.
    private static IReadOnlyList<ConfluenceZone> CorrectiveConfluence(
        IReadOnlyList<decimal> p, StructureKind kind, int unfolding, FibScale scale)
    {
        if (kind is StructureKind.Zigzag or StructureKind.Flat)
        {
            return unfolding switch
            {
                2 => FibConfluenceCalculator.EntryZones([new FibLeg(p[0], p[1], "Wave A", 1.0m)], scale),
                3 => FibConfluenceCalculator.TargetZones([new FibLeg(p[0], p[1], "Wave A", 1.0m)], p[2], scale),
                _ => FibConfluenceCalculator.EntryZones([new FibLeg(p[0], p[3], "Waves A–C", 1.0m)], scale),
            };
        }

        if (kind == StructureKind.Triangle && unfolding is >= 2 and <= 5)
        {
            return FibConfluenceCalculator.EntryZones(
                [new FibLeg(p[unfolding - 2], p[unfolding - 1], "Prior leg", 1.0m)], scale);
        }

        return [];
    }

    // Wave B unfolding: retraces A. A zigzag B must hold A's origin (hard); a flat B is
    // expected to reach at least ~90% of A and may overshoot the origin, so no hard line.
    private static WaveLevels CorrectionWaveB(IReadOnlyList<decimal> p, bool bull, StructureKind kind)
    {
        var legA = p[1] - p[0];
        var zone = kind == StructureKind.Zigzag
            ? Zone(
                Retrace(p[1], legA, 0.5m), Retrace(p[1], legA, 0.618m),
                "Wave B target (50–61.8% of Wave A)", "Fibonacci retracement of Wave A")
            : Zone(
                Retrace(p[1], legA, 0.9m), Retrace(p[1], legA, 1.05m),
                "Wave B target (90–105% of Wave A)", "Flat: B revisits Wave A's origin");

        return new WaveLevels(
            "Wave B", bull,
            kind == StructureKind.Zigzag
                ? new PriceLevel(p[0], Side(bull), "Invalidation — Wave B must hold Wave A's origin", "Origin (start of Wave A)")
                : null,
            zone,
            [],
            new AlternativeScenario(
                kind == StructureKind.Zigzag ? "Flat or new trend" : "Correction already over",
                kind == StructureKind.Zigzag
                    ? "A retrace beyond A's origin argues for a flat correction or a trend reversal instead."
                    : "A shallow B (< 90% of A) argues for a zigzag rather than a flat."));
    }

    // Wave C unfolding: travels from B's end in A's direction, typically 1.0–1.618× A.
    private static WaveLevels CorrectionWaveC(IReadOnlyList<decimal> p, bool bull)
    {
        var legA = p[1] - p[0];
        var target = Zone(
            Project(p[2], legA, 1.0m), Project(p[2], legA, 1.618m),
            "Wave C target (1.0–1.618× Wave A)", "Fibonacci projection of Wave A from Wave B");

        // C travels in the structure's own direction, so the line that kills the C-wave
        // reading sits behind it: below price for a rising C, above for a falling one.
        return new WaveLevels(
            "Wave C", bull,
            new PriceLevel(p[2], Side(bull), "Invalidation — Wave C must hold Wave B's end", "End of Wave B"),
            null,
            [target],
            new AlternativeScenario("Correction already complete",
                "If price reverses through Wave B's end, the correction likely finished at Wave B."));
    }

    // A-B-C complete: expect the larger trend to resume; a push beyond C reopens the correction.
    private static WaveLevels CorrectionComplete(IReadOnlyList<decimal> p, bool bull)
    {
        var whole = p[3] - p[0];
        var target = Zone(
            Retrace(p[3], whole, 0.618m), Retrace(p[3], whole, 1.0m),
            "Recovery target (61.8–100% of the correction)", "Retracement of Waves A–C");

        return new WaveLevels(
            "Correction complete", bull,
            new PriceLevel(p[3], Side(bull), "Invalidation — a break beyond Wave C reopens the correction", "End of Wave C"),
            null,
            [target],
            new AlternativeScenario("Complex correction (W-X-Y)",
                "If price extends beyond Wave C, the correction may be evolving into a double three."));
    }

    // Triangle leg k unfolding: legs contract, so the leg must stay short of the terminal of
    // the leg two before it — that terminal is the hard line.
    private static WaveLevels TriangleLeg(IReadOnlyList<decimal> p, bool bull, int leg)
    {
        var names = new[] { "", "", "Wave B", "Wave C", "Wave D", "Wave E" };
        var prior = p[leg - 1] - p[leg - 2]; // the leg just completed; the new leg retraces it
        var zone = Zone(
            Retrace(p[leg - 1], prior, 0.618m), Retrace(p[leg - 1], prior, 0.786m),
            $"{names[leg]} target (61.8–78.6% of the prior leg)", "Typical triangle contraction");

        // The unfolding leg travels opposite to the just-completed leg; it must not pass the
        // terminal of the same-direction leg two back (contraction), which for wave B is the
        // origin itself.
        var barrier = p[leg - 2];
        var legUp = prior < 0; // retracing a down-leg means the new leg points up

        return new WaveLevels(
            $"{names[leg]} (triangle)", bull,
            new PriceLevel(
                barrier, Side(!legUp),
                $"Invalidation — {names[leg]} must stay inside the contracting range", "Terminal two legs back"),
            zone,
            [],
            new AlternativeScenario("Not a triangle",
                "A leg that leaves the contracting range voids the triangle count (zigzag or flat instead)."));
    }

    // Five legs complete: expect the post-triangle thrust, roughly the widest leg's span.
    private static WaveLevels TriangleThrust(IReadOnlyList<decimal> p, bool bull)
    {
        var widest = Math.Abs(p[1] - p[0]);
        // The thrust breaks opposite to the triangle's own A-leg direction (a wave-4 triangle
        // in a bull market opens with a down A; the thrust resumes upward).
        var thrustUp = !bull;
        var from = p[5];
        var target = thrustUp
            ? Zone(from + 0.75m * widest, from + widest, "Thrust target (≈ widest leg)", "Post-triangle thrust")
            : Zone(from - widest, from - 0.75m * widest, "Thrust target (≈ widest leg)", "Post-triangle thrust");

        return new WaveLevels(
            "Triangle thrust", bull,
            new PriceLevel(p[3], Side(thrustUp), "Invalidation — a break through Wave C's end voids the thrust", "End of Wave C"),
            null,
            [target],
            new AlternativeScenario("Triangle extending",
                "If price stays inside the range, wave E may be subdividing further."));
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
