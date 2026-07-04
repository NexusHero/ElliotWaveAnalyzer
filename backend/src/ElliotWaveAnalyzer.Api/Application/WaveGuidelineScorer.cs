using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Scores a rule-valid Elliott structure 0..1 from the soft guidelines: Fibonacci proportion
/// fit, wave 2/4 alternation, channel linearity, and time proportionality. Weights come from
/// <see cref="WaveScoringOptions"/> and are renormalized over the components a structure kind
/// actually has (e.g. no channel for corrections).
///
/// Extracted from <see cref="WaveGrammarParser"/> so the parser keeps a single responsibility
/// (search) and the scoring (ranking) is a separate, independently-testable concern. Decoupled
/// from the parser's internal node type: alternation only needs each child's structure kind,
/// passed as <c>childKinds</c>, not the whole parse node. Pure and static — no I/O.
/// </summary>
internal static class WaveGuidelineScorer
{
    /// <summary>
    /// Scores <paramref name="kind"/> over its pivot <paramref name="points"/> (origin + wave
    /// terminals). <paramref name="childKinds"/> is the structure each child wave subdivides
    /// into (null for a terminal leg), used only for wave 2/4 alternation on motive structures.
    /// </summary>
    internal static double Score(
        StructureKind kind,
        IReadOnlyList<WaveAnnotation> points,
        IReadOnlyList<StructureKind?> childKinds,
        WaveScoringOptions opts)
    {
        var p = points.Select(a => (double)a.Price).ToArray();

        var components = new List<(double Weight, double Value)>
        {
            (opts.FibWeight, FibScore(kind, p, opts.FibTolerance)),
            (opts.TimeWeight, TimeScore(points)),
        };

        if (kind is StructureKind.Impulse or StructureKind.Diagonal)
        {
            components.Add((opts.AlternationWeight, AlternationScore(p, childKinds)));
        }
        if (kind is StructureKind.Impulse)
        {
            components.Add((opts.ChannelWeight, ChannelScore(points)));
        }

        var totalWeight = components.Sum(c => c.Weight);
        return totalWeight <= 0 ? 0 : components.Sum(c => c.Weight * c.Value) / totalWeight;
    }

    /// <summary>Distance of each characteristic ratio to its nearest canonical target,
    /// full credit at the target, none at <paramref name="tolerance"/> away.</summary>
    private static double FibScore(StructureKind kind, double[] p, double tolerance)
    {
        var scores = new List<double>();

        void Add(double numerator, double denominator, double[] targets)
        {
            if (denominator == 0)
            {
                return;
            }
            var ratio = Math.Abs(numerator) / Math.Abs(denominator);
            var distance = targets.Min(t => Math.Abs(ratio - t));
            scores.Add(Math.Max(0, 1 - distance / tolerance));
        }

        switch (kind)
        {
            case StructureKind.Impulse or StructureKind.Diagonal:
                Add(p[2] - p[1], p[1] - p[0], [0.382, 0.5, 0.618]);   // wave 2 retrace
                Add(p[3] - p[2], p[1] - p[0], [1.618, 2.0, 2.618]);   // wave 3 extension
                Add(p[4] - p[3], p[3] - p[2], [0.236, 0.382, 0.5]);   // wave 4 retrace
                Add(p[5] - p[4], p[1] - p[0], [0.618, 1.0]);          // wave 5 vs wave 1
                break;

            case StructureKind.Zigzag:
                Add(p[2] - p[1], p[1] - p[0], [0.382, 0.5, 0.618]);   // B retrace of A
                Add(p[3] - p[2], p[1] - p[0], [1.0, 1.618]);          // C vs A
                break;

            case StructureKind.Flat:
                Add(p[2] - p[1], p[1] - p[0], [1.0]);                 // B ≈ A
                Add(p[3] - p[2], p[1] - p[0], [1.0, 1.618]);          // C vs A
                break;

            default: // Triangle: each leg ≈ 0.618 of the leg two before it
                Add(p[3] - p[2], p[1] - p[0], [0.618]);
                Add(p[4] - p[3], p[2] - p[1], [0.618]);
                Add(p[5] - p[4], p[3] - p[2], [0.618]);
                break;
        }

        return scores.Count == 0 ? 0 : scores.Average();
    }

    /// <summary>Waves 2 and 4 should differ — in structure kind when both subdivide, and
    /// in retrace depth otherwise (sharp vs. sideways).</summary>
    private static double AlternationScore(double[] p, IReadOnlyList<StructureKind?> childKinds)
    {
        var wave1 = Math.Abs(p[1] - p[0]);
        var wave3 = Math.Abs(p[3] - p[2]);
        double depthScore = 0;
        if (wave1 > 0 && wave3 > 0)
        {
            var retrace2 = Math.Abs(p[2] - p[1]) / wave1;
            var retrace4 = Math.Abs(p[4] - p[3]) / wave3;
            depthScore = Math.Min(1, Math.Abs(retrace2 - retrace4) / 0.3);
        }

        var kind2 = childKinds[1];
        var kind4 = childKinds[3];
        if (kind2 is not null && kind4 is not null)
        {
            return kind2 != kind4 ? 1 : Math.Min(depthScore, 0.25);
        }

        return depthScore;
    }

    /// <summary>Linearity (R²) of the wave 1/3/5 terminals — a straight upper channel
    /// line is the classic sign of a well-formed impulse.</summary>
    private static double ChannelScore(IReadOnlyList<WaveAnnotation> points)
    {
        var origin = points[0].Date;
        (double X, double Y)[] terminals =
        [
            ((points[1].Date - origin).TotalDays, (double)points[1].Price),
            ((points[3].Date - origin).TotalDays, (double)points[3].Price),
            ((points[5].Date - origin).TotalDays, (double)points[5].Price),
        ];

        var meanX = terminals.Average(t => t.X);
        var meanY = terminals.Average(t => t.Y);
        var covXY = terminals.Sum(t => (t.X - meanX) * (t.Y - meanY));
        var varX = terminals.Sum(t => (t.X - meanX) * (t.X - meanX));
        var varY = terminals.Sum(t => (t.Y - meanY) * (t.Y - meanY));

        if (varX == 0 || varY == 0)
        {
            return 1; // degenerate but perfectly "linear"
        }

        var r = covXY / Math.Sqrt(varX * varY);
        return r * r;
    }

    /// <summary>No wave should dwarf its siblings in duration: full credit while the
    /// longest/shortest wave duration ratio stays ≤ 8, fading to zero at ≥ 30.</summary>
    private static double TimeScore(IReadOnlyList<WaveAnnotation> points)
    {
        var durations = new List<double>();
        for (var k = 1; k < points.Count; k++)
        {
            durations.Add(Math.Max(1, (points[k].Date - points[k - 1].Date).TotalDays));
        }

        var ratio = durations.Max() / durations.Min();
        return Math.Clamp((30 - ratio) / (30 - 8), 0, 1);
    }
}
