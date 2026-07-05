using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The deterministic re-verification of an analyst-edited count (REQ-031): snap the edited pivots to
/// real candle extremes (so a dragged pivot lands on real data), then run the hard rules, the forward
/// projections and a guideline score on what snapped. Pure and static — annotations + candles in, an
/// objective <see cref="WaveVerification"/> out, no LLM, no I/O. The geometry stays deterministic; the
/// LLM is never in this path (ADR-009).
/// </summary>
public static class WaveVerifier
{
    /// <summary>Verifies <paramref name="annotations"/> against <paramref name="candles"/>.</summary>
    public static WaveVerification Verify(
        IReadOnlyList<WaveAnnotation> annotations, IReadOnlyList<MarketCandle> candles)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        ArgumentNullException.ThrowIfNull(candles);

        // Snap each edited pivot to the nearest candle extreme; ones that don't land on real data are
        // reported (never silently trusted), exactly like the vision-import guard.
        var claimed = annotations
            .OrderBy(a => a.Date)
            .Select(a => new ClaimedPivot(a.Date, a.Price, a.Label))
            .ToList();
        var (snapped, rejected) = PivotSnapper.Snap(claimed, candles);

        var snappedAnnotations = snapped
            .OrderBy(s => s.Date)
            .Select(s => new WaveAnnotation(s.Date, s.Price, s.Label))
            .ToList();

        var rules = ElliottRuleChecker.Check(snappedAnnotations);
        var levels = ProjectionService.Project(snappedAnnotations);
        var structure = InferStructure(snappedAnnotations.Select(a => a.Label));
        var score = ScoreOrNull(structure, snappedAnnotations);

        // Only a hard-rule Fail invalidates; a failed guideline merely flavours the count.
        var isValid = rules.Rules.All(r => r.Status != RuleStatus.Fail || r.IsGuideline);

        return new WaveVerification(
            structure, rules.BullishAssumed, isValid, snapped, rejected, rules, levels, score);
    }

    /// <summary>Infers the structure family from the drawn labels (mirrors the vision-import inference).</summary>
    private static string InferStructure(IEnumerable<string> labels)
    {
        var set = labels.Select(l => l.Trim().ToUpperInvariant()).ToHashSet();
        if (set.Contains("5") || set.Contains("4") || set.Contains("3"))
        {
            return "Impulse";
        }

        return set.Contains("C") || set.Contains("B") ? "Corrective" : "Unknown";
    }

    /// <summary>
    /// A guideline score for the edited count when its structure and pivot count line up, else null.
    /// Defensive: scoring is a ranking aid, so any shape it can't handle degrades to "no score" rather
    /// than failing the whole verification.
    /// </summary>
    private static double? ScoreOrNull(string structure, IReadOnlyList<WaveAnnotation> annotations)
    {
        try
        {
            var opts = new WaveScoringOptions();
            return structure switch
            {
                "Impulse" when annotations.Count >= 6 => WaveGuidelineScorer.Score(
                    StructureKind.Impulse, annotations.Take(6).ToList(), TerminalChildren(5), opts),
                "Corrective" when annotations.Count >= 4 => WaveGuidelineScorer.Score(
                    StructureKind.Zigzag, annotations.Take(4).ToList(), TerminalChildren(3), opts),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>A list of <paramref name="waves"/> terminal (unsubdivided) child kinds.</summary>
    private static IReadOnlyList<StructureKind?> TerminalChildren(int waves)
        => Enumerable.Repeat<StructureKind?>(null, waves).ToList();
}
