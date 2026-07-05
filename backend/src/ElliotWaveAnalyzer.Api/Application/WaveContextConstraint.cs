using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Applies a parent <see cref="WaveContext"/> to a finer timeframe's candidate counts: candidates
/// travelling against the parent's direction are <em>hard-rejected</em> (they cannot possibly be
/// the substructure of that wave); the survivors are re-scored with <em>soft</em> penalties for a
/// class mismatch or a price range that spills outside the parent window, then re-ranked. The
/// per-link consistency verdict describes how well the best survivor fits. Pure and deterministic.
/// </summary>
public static class WaveContextConstraint
{
    /// <summary>The re-ranked survivors (contradicting candidates removed), the verdict for the
    /// best survivor, and a human-readable reason.</summary>
    public readonly record struct Result(
        IReadOnlyList<WaveCandidate> Ranked, ConsistencyVerdict Verdict, string Reason);

    /// <summary>
    /// Constrains <paramref name="candidates"/> to the parent <paramref name="context"/>.
    /// </summary>
    public static Result Apply(
        WaveContext context, IReadOnlyList<WaveCandidate> candidates, WaveScoringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(candidates);
        var opts = options ?? WaveScoringOptions.Default;

        // Hard reject: a count that nets the wrong way cannot be this wave's substructure.
        var survivors = candidates
            .Where(c => c.Waves.Count > 0 && DirectionOf(c) == context.ExpectedDirection)
            .ToList();

        if (survivors.Count == 0)
        {
            return new Result(
                [],
                ConsistencyVerdict.Contradiction,
                $"No finer count travels {Word(context.ExpectedDirection)} inside {context.ParentWaveLabel}; "
                + "every candidate contradicts the higher-timeframe direction.");
        }

        var ranked = survivors
            .Select(c => c with { Score = Penalized(c, context, opts) })
            .OrderByDescending(c => c.Score ?? 0m)
            .ThenByDescending(c => c.Waves[^1].Date)
            .ToList();

        var best = ranked[0];
        var classMatches = ClassOf(best) == context.ExpectedClass;
        var inWindow = WithinWindow(best, context, opts);

        if (classMatches && inWindow)
        {
            return new Result(
                ranked,
                ConsistencyVerdict.Consistent,
                $"Finer count is a {Word(context.ExpectedClass)} move {Word(context.ExpectedDirection)} "
                + $"inside {context.ParentWaveLabel}'s price window — consistent.");
        }

        var reasons = new List<string>();
        if (!classMatches)
        {
            reasons.Add(
                $"expected a {Word(context.ExpectedClass)} substructure but the best count is {Word(ClassOf(best))}");
        }

        if (!inWindow)
        {
            reasons.Add("its price range spills outside the parent wave's window");
        }

        return new Result(
            ranked,
            ConsistencyVerdict.Tension,
            $"Direction agrees with {context.ParentWaveLabel}, but {string.Join(" and ", reasons)}.");
    }

    private static decimal Penalized(WaveCandidate c, WaveContext ctx, WaveScoringOptions opts)
    {
        var score = c.Score ?? 0.5m;
        if (ClassOf(c) != ctx.ExpectedClass)
        {
            score *= (decimal)opts.TopDownClassMismatchPenalty;
        }

        if (!WithinWindow(c, ctx, opts))
        {
            score *= (decimal)opts.TopDownOutOfWindowPenalty;
        }

        return score;
    }

    private static TrendDirection DirectionOf(WaveCandidate c)
        => c.Waves[^1].Price >= c.Origin.Price ? TrendDirection.Up : TrendDirection.Down;

    private static StructureClass ClassOf(WaveCandidate c)
        => c.Structure is "Impulse" or "Diagonal" ? StructureClass.Motive : StructureClass.Corrective;

    private static bool WithinWindow(WaveCandidate c, WaveContext ctx, WaveScoringOptions opts)
    {
        var prices = new List<decimal> { c.Origin.Price };
        prices.AddRange(c.Waves.Select(w => w.Price));
        var tolerance = (ctx.WindowHigh - ctx.WindowLow) * (decimal)opts.TopDownWindowTolerance;
        return prices.Min() >= ctx.WindowLow - tolerance && prices.Max() <= ctx.WindowHigh + tolerance;
    }

    private static string Word(TrendDirection d) => d == TrendDirection.Up ? "up" : "down";

    private static string Word(StructureClass c) => c == StructureClass.Motive ? "motive" : "corrective";
}
