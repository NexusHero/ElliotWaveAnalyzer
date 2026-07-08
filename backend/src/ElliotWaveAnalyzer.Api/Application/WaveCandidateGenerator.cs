using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Turns a stream of swing pivots into rule-valid candidate impulse counts. Slides a
/// six-pivot window (origin + five wave terminals) across the pivots and keeps only those
/// that pass the deterministic Elliott rule check — so every candidate handed to the LLM is
/// already geometrically sound. This is the deterministic half of the hybrid auto-counter;
/// the LLM half (ranking + explanation) sits in <c>IAutoWaveAnalyzer</c>.
///
/// Scope (v1): impulses only. ABC corrections need bespoke rules the deterministic checker
/// does not yet model, so they are deliberately left to a follow-up.
///
/// Pure (static, no I/O) so it is fully unit-testable.
/// </summary>
public static class WaveCandidateGenerator
{
    /// <summary>How many candidates to keep — caps prompt size / token cost.</summary>
    private const int MaxCandidates = 6;

    /// <summary>Six pivots = origin + the five wave terminals 1..5.</summary>
    private const int ImpulsePivotCount = 6;

    /// <summary>
    /// Generates rule-valid impulse candidates, most recent first, capped to keep the
    /// downstream LLM prompt small. <see cref="WaveCandidate.Id"/> is assigned 0..n-1 in the
    /// returned order so the LLM can reference candidates by id.
    /// </summary>
    public static IReadOnlyList<WaveCandidate> Generate(IReadOnlyList<SwingPivot> pivots)
    {
        ArgumentNullException.ThrowIfNull(pivots);

        var candidates = new List<WaveCandidate>();
        if (pivots.Count < ImpulsePivotCount)
        {
            return candidates;
        }

        for (var i = 0; i + ImpulsePivotCount <= pivots.Count; i++)
        {
            var window = pivots.Skip(i).Take(ImpulsePivotCount).ToList();

            // ZigZag output already alternates, but guard anyway: an impulse leg must.
            if (!Alternates(window))
            {
                continue;
            }

            var origin = new WaveAnnotation(window[0].Date, window[0].Price, "1");
            var waves = new List<WaveAnnotation>
            {
                new(window[1].Date, window[1].Price, "1"),
                new(window[2].Date, window[2].Price, "2"),
                new(window[3].Date, window[3].Price, "3"),
                new(window[4].Date, window[4].Price, "4"),
                new(window[5].Date, window[5].Price, "5"),
            };

            // Labels are positional placeholders here; ElliottRuleChecker reads pivots by
            // position (origin first), not by label, so the check is geometric.
            var countPivots = new List<WaveAnnotation> { origin };
            countPivots.AddRange(waves);

            var report = ElliottRuleChecker.Check(countPivots);
            if (report.Rules.Any(r => r is { Status: RuleStatus.Fail, IsGuideline: false }))
            {
                continue; // hard-rule violations disqualify; failed guidelines only flavor
            }

            var levels = ProjectionService.Project(countPivots);
            candidates.Add(new WaveCandidate(0, "Impulse", origin, waves, report, levels));
        }

        return candidates
            .OrderByDescending(c => c.Waves[^1].Date)
            .Take(MaxCandidates)
            .Select((c, idx) => c with { Id = idx })
            .ToList();
    }

    /// <summary>
    /// Grammar-parser-backed candidate generation: nested, multi-structure counts (impulses,
    /// diagonals, zigzags, flats, triangles with subdivided waves) instead of the flat
    /// six-pivot impulse window of <see cref="Generate"/>. Candidates keep the same flat
    /// top-level shape (origin + labelled waves + rule report) for contract compatibility and
    /// additionally carry the parse tree and its deterministic score.
    /// </summary>
    /// <param name="pivots">Alternating pivots, finest scale, chronological.</param>
    /// <param name="options">Scoring weights and search bounds; null for defaults.</param>
    /// <param name="rootDegree">Degree assigned to top-level structures.</param>
    /// <param name="candles">
    /// The same candle series the pivots were detected from (#224). When supplied, each
    /// candidate's <see cref="WaveRuleReport"/> gains momentum-divergence and volume-signature
    /// guideline rows, and its <see cref="WaveCandidate.Score"/> is penalized the same way a
    /// failed guideline already is inside the parser (<see cref="WaveScoringOptions.GuidelinePenalty"/>).
    /// Computed once here — never inside the grammar parser's beam search — so indicator
    /// calculation cannot regress the search's performance (it can run thousands of partition
    /// evaluations per parse).
    /// </param>
    /// <param name="indicatorCalculator">
    /// Supplies RSI/MACD for the momentum guideline; omit to run only the volume guideline
    /// (volume needs no indicator calculation, only candle volume).
    /// </param>
    /// <param name="cancellationToken">Cancellation for the search budget.</param>
    /// <returns>The parsed candidates (best score first) and whether the search was
    /// truncated by the evaluation budget.</returns>
    public static (IReadOnlyList<WaveCandidate> Candidates, bool SearchTruncated) GenerateParsed(
        IReadOnlyList<SwingPivot> pivots,
        WaveScoringOptions? options = null,
        WaveDegree rootDegree = WaveDegree.Primary,
        IReadOnlyList<MarketCandle>? candles = null,
        IIndicatorCalculator? indicatorCalculator = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pivots);

        var parse = WaveGrammarParser.Parse(pivots, options, rootDegree, cancellationToken);
        var candidates = new List<WaveCandidate>(parse.Trees.Count);

        // Computed once for the whole candidate set, not per candidate — RSI/MACD are series over
        // the full window, not something a single wave's dates need recomputed.
        var rsi = candles is not null ? indicatorCalculator?.CalculateRsi(candles) : null;
        var macd = candles is not null ? indicatorCalculator?.CalculateMacd(candles) : null;
        var guidelinePenalty = (options ?? new WaveScoringOptions()).GuidelinePenalty;

        foreach (var (tree, index) in parse.Trees.Select((t, idx) => (t, idx)))
        {
            var root = tree.Root;
            var origin = root.Start with { Label = "1" };
            var waves = root.Children
                .Select(c => new WaveAnnotation(c.End.Date, c.End.Price, c.Label))
                .ToList();

            var isMotive = root.Kind is StructureKind.Impulse or StructureKind.Diagonal;
            var levels = isMotive
                ? ProjectionService.Project([origin, .. waves])
                : ProjectionService.ProjectCorrective([origin, .. waves], root.Kind!.Value);

            var report = root.RuleReport!;
            var score = tree.Score;

            // Momentum/volume checkers only understand a 5-wave impulse or a simple ABC correction
            // (Zigzag/Flat) — a Triangle's 5 legs would coincidentally match the impulse pivot count
            // but mean something else entirely, so it is deliberately excluded here.
            var understood = root.Kind is StructureKind.Impulse or StructureKind.Diagonal
                or StructureKind.Zigzag or StructureKind.Flat;
            if (candles is not null && understood)
            {
                List<WaveAnnotation> pivotsForCheck = [origin, .. waves];
                var momentum = MomentumDivergenceChecker.Check(pivotsForCheck, rsi, macd);
                var volume = VolumeGuidelineChecker.Check(pivotsForCheck, candles);
                report = report with { Rules = [.. report.Rules, momentum, volume] };

                if (momentum.Status == RuleStatus.Fail)
                {
                    score *= (decimal)guidelinePenalty;
                }
                if (volume.Status == RuleStatus.Fail)
                {
                    score *= (decimal)guidelinePenalty;
                }
            }

            candidates.Add(new WaveCandidate(
                index, root.Kind!.Value.ToString(), origin, waves, report, levels)
            {
                Tree = root,
                Score = score,
            });
        }

        return (candidates, parse.SearchTruncated);
    }

    private static bool Alternates(IReadOnlyList<SwingPivot> window)
    {
        for (var i = 1; i < window.Count; i++)
        {
            if (window[i].IsHigh == window[i - 1].IsHigh)
            {
                return false;
            }
        }

        return true;
    }
}
