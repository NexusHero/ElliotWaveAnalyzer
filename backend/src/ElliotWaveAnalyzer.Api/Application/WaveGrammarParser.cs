using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Recursive Elliott Wave grammar parser — the deterministic core of the auto-counter.
///
/// The Elliott rulebook is treated as a grammar over the alternating pivot sequence:
/// <code>
/// Motive     → Impulse | Diagonal
/// Impulse    → 1:Motive 2:Corrective 3:Motive 4:Corrective 5:Motive
/// Diagonal   → five legs, overlap allowed, wedge contraction
/// Corrective → Zigzag | Flat | Triangle
/// Zigzag     → A:Motive B:Corrective C:Motive        (5-3-5)
/// Flat       → A:Corrective B:Corrective C:Motive    (3-3-5)
/// Triangle   → A..E, each Corrective                 (3-3-3-3-3)
/// </code>
/// A wave spanning exactly one leg is a terminal; a wave spanning three or more legs must
/// itself parse as a structure of the allowed family — that recursion is what produces
/// nested, multi-degree counts. Parsing is dynamic programming over pivot intervals
/// (memoized per structure kind × interval, CYK-style) with the hard rule checkers pruning
/// partitions immediately and the guideline score ranking survivors inside a fixed beam.
///
/// NOTE on multi-scale input: a structure over coarse pivots is exactly a structure over the
/// matching subset of fine pivots (the multi-scale subset invariant), so parsing the finest
/// pivot sequence with recursive subdivision subsumes parsing each scale separately. Feed
/// this parser the finest scale; degrees fall out of tree depth.
///
/// Pure (static, no I/O) so it is fully unit-testable. Deterministic: identical input and
/// options produce identical output, including when the evaluation budget truncates the
/// search (enumeration order is fixed).
/// </summary>
public static class WaveGrammarParser
{
    private static readonly StructureKind[] AllKinds =
        [StructureKind.Impulse, StructureKind.Diagonal, StructureKind.Zigzag, StructureKind.Flat, StructureKind.Triangle];

    private static readonly StructureKind[] MotiveKinds = [StructureKind.Impulse, StructureKind.Diagonal];

    private static readonly StructureKind[] CorrectiveKinds =
        [StructureKind.Zigzag, StructureKind.Flat, StructureKind.Triangle];

    /// <summary>How many trailing pivots may end a top-level structure (the count of interest
    /// is the one unfolding now, not one buried mid-history).</summary>
    private const int RecentEndWindow = 3;

    /// <summary>Neutral score carried by terminal (unsubdivided) legs.</summary>
    private const double TerminalScore = 0.5;

    /// <summary>
    /// Parses the pivot sequence into ranked, nested Elliott Wave counts.
    /// </summary>
    /// <param name="pivots">Alternating high/low pivots, chronological (finest scale). When
    /// longer than <see cref="WaveScoringOptions.MaxPivots"/>, only the most recent pivots
    /// are parsed.</param>
    /// <param name="options">Scoring weights and search bounds; null for defaults.</param>
    /// <param name="rootDegree">Degree assigned to top-level structures; children step down.</param>
    /// <param name="cancellationToken">Cooperative cancellation for the wall-clock budget.</param>
    public static WaveParseResult Parse(
        IReadOnlyList<SwingPivot> pivots,
        WaveScoringOptions? options = null,
        WaveDegree rootDegree = WaveDegree.Primary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pivots);
        var opts = options ?? WaveScoringOptions.Default;

        if (pivots.Count > opts.MaxPivots)
        {
            pivots = [.. pivots.Skip(pivots.Count - opts.MaxPivots)];
        }

        var ctx = new ParseContext(pivots, opts, cancellationToken);
        var roots = new List<ProtoNode>();

        for (var j = pivots.Count - 1; j >= 0 && j > pivots.Count - 1 - RecentEndWindow; j--)
        {
            foreach (var kind in AllKinds)
            {
                var minSpan = MinSpan(kind);
                for (var i = j - minSpan; i >= 0; i -= 2)
                {
                    roots.AddRange(ParseCell(ctx, kind, i, j));
                }
            }
        }

        var trees = roots
            .OrderByDescending(n => n.Score)
            .ThenByDescending(n => n.End)   // prefer counts reaching closer to the present
            .ThenBy(n => n.Start)           // then the larger structure
            .Take(opts.MaxCandidates)
            .Select(n => new ScoredWaveTree(Materialize(ctx, n, rootDegree), Round(n.Score)))
            .ToList();

        return new WaveParseResult(trees, ctx.Truncated);
    }

    // ─── grammar composition ──────────────────────────────────────────────────

    private static int MinSpan(StructureKind kind)
        => kind is StructureKind.Zigzag or StructureKind.Flat ? 3 : 5;

    private static int ChildCount(StructureKind kind)
        => kind is StructureKind.Zigzag or StructureKind.Flat ? 3 : 5;

    private static string[] ChildLabels(StructureKind kind) => kind switch
    {
        StructureKind.Zigzag or StructureKind.Flat => ["A", "B", "C"],
        StructureKind.Triangle => ["A", "B", "C", "D", "E"],
        _ => ["1", "2", "3", "4", "5"],
    };

    /// <summary>Which structure families each child slot may subdivide into.</summary>
    private static StructureKind[][] ChildRoles(StructureKind kind) => kind switch
    {
        StructureKind.Impulse or StructureKind.Diagonal =>
            [MotiveKinds, CorrectiveKinds, MotiveKinds, CorrectiveKinds, MotiveKinds],
        StructureKind.Zigzag => [MotiveKinds, CorrectiveKinds, MotiveKinds],
        StructureKind.Flat => [CorrectiveKinds, CorrectiveKinds, MotiveKinds],
        _ => [CorrectiveKinds, CorrectiveKinds, CorrectiveKinds, CorrectiveKinds, CorrectiveKinds],
    };

    private static WaveRuleReport CheckRules(StructureKind kind, IReadOnlyList<WaveAnnotation> points)
        => kind switch
        {
            StructureKind.Impulse => ElliottRuleChecker.Check(points),
            StructureKind.Diagonal => DiagonalRuleChecker.Check(points),
            StructureKind.Zigzag => ZigzagRuleChecker.Check(points),
            StructureKind.Flat => FlatRuleChecker.Check(points),
            _ => TriangleRuleChecker.Check(points),
        };

    /// <summary>
    /// Best parses of <paramref name="kind"/> over pivots [i..j], memoized. Returns up to
    /// BeamWidth trees, best first; empty when the interval cannot host the structure.
    /// </summary>
    private static IReadOnlyList<ProtoNode> ParseCell(ParseContext ctx, StructureKind kind, int i, int j)
    {
        var span = j - i;
        if (i < 0 || span < MinSpan(kind) || span % 2 == 0)
        {
            return [];
        }

        var key = (kind, i, j);
        if (ctx.Memo.TryGetValue(key, out var cached))
        {
            return cached;
        }

        ctx.CancellationToken.ThrowIfCancellationRequested();

        var results = new List<ProtoNode>();
        var boundaries = new int[ChildCount(kind) + 1];
        boundaries[0] = i;
        boundaries[^1] = j;
        EnumeratePartitions(ctx, kind, boundaries, level: 1, results);

        var kept = (IReadOnlyList<ProtoNode>)results
            .OrderByDescending(n => n.Score)
            .Take(ctx.Options.BeamWidth)
            .ToList();
        ctx.Memo[key] = kept;
        return kept;
    }

    /// <summary>
    /// Fills boundary slots left to right (each two pivot indices apart to preserve high/low
    /// parity), evaluating every complete partition. Feasibility bounds keep at least one leg
    /// per remaining child and cap each wave at MaxWaveSpanLegs.
    /// </summary>
    private static void EnumeratePartitions(
        ParseContext ctx, StructureKind kind, int[] boundaries, int level, List<ProtoNode> results)
    {
        if (ctx.Truncated)
        {
            return;
        }

        var childCount = boundaries.Length - 1;
        if (level == childCount)
        {
            // Last child's span is fixed by the previous boundary; only its cap needs checking.
            if (boundaries[^1] - boundaries[^2] <= ctx.Options.MaxWaveSpanLegs)
            {
                EvaluatePartition(ctx, kind, boundaries, results);
            }
            return;
        }

        var start = boundaries[level - 1];
        var remainingChildren = childCount - level;
        var maxBoundary = boundaries[^1] - remainingChildren;

        for (var b = start + 1; b <= maxBoundary; b += 2)
        {
            if (b - start > ctx.Options.MaxWaveSpanLegs)
            {
                break;
            }

            boundaries[level] = b;
            EnumeratePartitions(ctx, kind, boundaries, level + 1, results);
            if (ctx.Truncated)
            {
                return;
            }
        }
    }

    private static void EvaluatePartition(
        ParseContext ctx, StructureKind kind, int[] boundaries, List<ProtoNode> results)
    {
        if (++ctx.Evaluations > ctx.Options.MaxEvaluations)
        {
            ctx.Truncated = true;
            return;
        }

        var points = new WaveAnnotation[boundaries.Length];
        for (var k = 0; k < boundaries.Length; k++)
        {
            var pivot = ctx.Pivots[boundaries[k]];
            points[k] = new WaveAnnotation(pivot.Date, pivot.Price, "1");
        }

        var report = CheckRules(kind, points);
        if (report.Rules.Any(r => r is { Status: RuleStatus.Fail, IsGuideline: false }))
        {
            return; // hard rules prune — a violating partition never enters the beam
        }

        var labels = ChildLabels(kind);
        var roles = ChildRoles(kind);
        var children = new ProtoNode[boundaries.Length - 1];
        for (var k = 0; k < children.Length; k++)
        {
            var childStart = boundaries[k];
            var childEnd = boundaries[k + 1];
            if (childEnd - childStart == 1)
            {
                children[k] = new ProtoNode(
                    labels[k], null, childStart, childEnd, null, [], TerminalScore);
                continue;
            }

            // A multi-leg wave must itself parse as one of its role's structure families.
            ProtoNode? best = null;
            foreach (var subKind in roles[k])
            {
                var sub = ParseCell(ctx, subKind, childStart, childEnd);
                if (sub.Count > 0 && (best is null || sub[0].Score > best.Score))
                {
                    best = sub[0];
                }
            }

            if (best is null)
            {
                return; // no valid subdivision — the partition dies as a whole
            }

            children[k] = best with { Label = labels[k] };
        }

        var ownScore = WaveGuidelineScorer.Score(
            kind, points, [.. children.Select(c => c.Kind)], ctx.Options);
        var childAverage = children.Average(c => c.Score);
        var score = ownScore * (1 - ctx.Options.ChildScoreWeight)
            + childAverage * ctx.Options.ChildScoreWeight;

        var failedGuidelines = report.Rules.Count(r => r is { Status: RuleStatus.Fail, IsGuideline: true });
        for (var g = 0; g < failedGuidelines; g++)
        {
            score *= ctx.Options.GuidelinePenalty;
        }

        results.Add(new ProtoNode(
            kind.ToString(), kind, boundaries[0], boundaries[^1], report, children, score));
    }

    // ─── materialization ──────────────────────────────────────────────────────

    /// <summary>Converts the internal parse node into the public tree, assigning degrees by
    /// depth (root at <paramref name="degree"/>, children one step smaller, floored at Minute).</summary>
    private static WaveNode Materialize(ParseContext ctx, ProtoNode node, WaveDegree degree)
    {
        var childDegree = degree > WaveDegree.Minute ? degree - 1 : WaveDegree.Minute;
        var start = ctx.Pivots[node.Start];
        var end = ctx.Pivots[node.End];

        return new WaveNode(
            node.Label,
            node.Kind,
            degree,
            new WaveAnnotation(start.Date, start.Price, node.Label),
            new WaveAnnotation(end.Date, end.Price, node.Label),
            node.Report,
            Round(node.Score),
            [.. node.Children.Select(c => Materialize(ctx, c, childDegree))]);
    }

    private static decimal Round(double score) => Math.Round((decimal)score, 4);

    /// <summary>Mutable per-parse state: pivots, options, memo table and the search budget.</summary>
    private sealed class ParseContext(
        IReadOnlyList<SwingPivot> pivots, WaveScoringOptions options, CancellationToken cancellationToken)
    {
        public IReadOnlyList<SwingPivot> Pivots { get; } = pivots;
        public WaveScoringOptions Options { get; } = options;
        public CancellationToken CancellationToken { get; } = cancellationToken;
        public Dictionary<(StructureKind Kind, int Start, int End), IReadOnlyList<ProtoNode>> Memo { get; } = [];
        public long Evaluations;
        public bool Truncated;
    }

    /// <summary>Parse-internal tree node holding pivot indices instead of prices/dates.</summary>
    private sealed record ProtoNode(
        string Label,
        StructureKind? Kind,
        int Start,
        int End,
        WaveRuleReport? Report,
        IReadOnlyList<ProtoNode> Children,
        double Score);
}
