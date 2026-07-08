using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The pure heart of Phase 3: turns per-timeframe pivots (coarsest → finest) into a single
/// top-down read. The coarsest timeframe is parsed freely; every finer timeframe is parsed and
/// then <em>constrained</em> to the wave unfolding on the timeframe above it — counts that travel
/// the wrong way are rejected, counts whose class or price range disagrees are penalized. Each
/// adjacent link gets a consistency verdict. No I/O and no LLM: identical pivots produce an
/// identical chain, so the result serializes deterministically.
/// </summary>
public static class TopDownWaveAnalyzer
{
    /// <summary>
    /// Analyzes the timeframes (which must be ordered coarsest → finest). Timeframes below a hard
    /// contradiction re-establish their own local context so the chain always continues.
    /// </summary>
    public static TopDownAnalysis Analyze(
        IReadOnlyList<TimeframePivots> timeframesCoarseToFine,
        WaveScoringOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeframesCoarseToFine);
        var opts = options ?? WaveScoringOptions.Default;

        var counts = new List<TimeframeCount>();
        var links = new List<TimeframeConsistency>();
        WaveContext? parentContext = null;
        string? parentInterval = null;

        for (var i = 0; i < timeframesCoarseToFine.Count; i++)
        {
            var tf = timeframesCoarseToFine[i];
            var degree = DegreeForLevel(i);
            var (candidates, truncated) =
                WaveCandidateGenerator.GenerateParsed(tf.Pivots, opts, degree, cancellationToken: cancellationToken);

            WaveCandidate? best;
            if (parentContext is null)
            {
                best = candidates.Count > 0 ? candidates[0] : null;
            }
            else
            {
                var constrained = WaveContextConstraint.Apply(parentContext, candidates, opts);
                links.Add(new TimeframeConsistency(
                    parentInterval!, tf.Interval, constrained.Verdict, constrained.Reason));

                // Prefer the best surviving (consistent-direction) count; fall back to the raw best
                // so a contradicted timeframe still shows *a* count for context, honestly flagged.
                best = constrained.Ranked.Count > 0
                    ? constrained.Ranked[0]
                    : candidates.Count > 0 ? candidates[0] : null;
            }

            var imposed = best is null ? null : WaveContextDeriver.Derive(best);
            counts.Add(new TimeframeCount(tf.Interval, degree, best, imposed, truncated));

            parentContext = imposed;
            parentInterval = tf.Interval;
        }

        return new TopDownAnalysis(counts, links, BuildSummary(counts, links));
    }

    // Primary at the top, stepping one degree smaller per finer timeframe, floored at Minute.
    private static WaveDegree DegreeForLevel(int level)
    {
        var value = Math.Max((int)WaveDegree.Minute, (int)WaveDegree.Primary - level);
        return (WaveDegree)value;
    }

    private static string BuildSummary(
        IReadOnlyList<TimeframeCount> counts, IReadOnlyList<TimeframeConsistency> links)
    {
        var verdictByChild = links.ToDictionary(l => l.ChildInterval, l => l.Verdict);
        var parts = new List<string>(counts.Count);

        foreach (var tf in counts)
        {
            var structure = tf.BestCount?.Structure ?? "no count";
            var context = tf.ImposedContext is { } c
                ? $" ({c.ParentWaveLabel} forming, {(c.ExpectedDirection == TrendDirection.Up ? "up" : "down")})"
                : string.Empty;
            var verdict = verdictByChild.TryGetValue(tf.Interval, out var v) ? $" [{v}]" : string.Empty;
            parts.Add(string.Create(
                CultureInfo.InvariantCulture, $"{tf.Interval}: {structure}{context}{verdict}"));
        }

        return string.Join(" → ", parts);
    }
}
