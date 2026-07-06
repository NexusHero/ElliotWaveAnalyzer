using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The deterministic half of alternate-hypothesis generation (#186): given a structure the LLM
/// proposed, generate it over the detected pivots and rule-check it with the <em>same</em> checkers
/// the beam parser uses, then score a survivor with the same guideline scorer. The LLM never asserts a
/// count — it only names a structure to test; this validator owns whether that structure is rule-valid
/// over the geometry. Pure and static: pivots in, an objective <see cref="HypothesisResult"/> out.
/// </summary>
public static class HypothesisValidator
{
    /// <summary>
    /// Validates a proposed <paramref name="kind"/> over <paramref name="pivots"/> (ascending by date):
    /// the most recent pivots that could form the structure are rule-checked; a hard-rule failure is
    /// reported as rejected with the failing rule, and a survivor carries its guideline score.
    /// </summary>
    public static HypothesisResult Validate(
        StructureKind kind, string reason, IReadOnlyList<SwingPivot> pivots)
    {
        ArgumentNullException.ThrowIfNull(pivots);

        var need = RequiredPivots(kind);
        if (pivots.Count < need)
        {
            return new HypothesisResult(
                kind.ToString(), reason, IsValid: false, Score: null,
                FailingRule: $"Needs {need} pivots to form a {kind}; only {pivots.Count} detected.");
        }

        // The most recent `need` pivots are the current candidate for this structure. Labels are only
        // for display — every rule checker reads pivot prices by position — so the scheme is cosmetic.
        var window = pivots
            .OrderBy(p => p.Date)
            .Skip(pivots.Count - need)
            .Select((p, i) => new WaveAnnotation(p.Date, p.Price, LabelFor(kind, i)))
            .ToList();

        var report = CheckFor(kind, window);
        var failing = report.Rules.FirstOrDefault(r => r.Status == RuleStatus.Fail && !r.IsGuideline);
        if (failing is not null)
        {
            return new HypothesisResult(kind.ToString(), reason, IsValid: false, Score: null, failing.Name);
        }

        return new HypothesisResult(kind.ToString(), reason, IsValid: true, Score: ScoreOrNull(kind, window), null);
    }

    // Pivots a structure needs (origin + waves): motive is 6 (0..5), a three is 4 (0,A,B,C),
    // a triangle is 6 (0,A,B,C,D,E).
    private static int RequiredPivots(StructureKind kind) => kind switch
    {
        StructureKind.Impulse or StructureKind.Diagonal or StructureKind.Triangle => 6,
        StructureKind.Zigzag or StructureKind.Flat => 4,
        _ => 6,
    };

    // Rule-check the exact structure with the same positional checkers the beam parser uses.
    private static WaveRuleReport CheckFor(StructureKind kind, IReadOnlyList<WaveAnnotation> a) => kind switch
    {
        StructureKind.Impulse => ElliottRuleChecker.Check(a),
        StructureKind.Diagonal => DiagonalRuleChecker.Check(a),
        StructureKind.Zigzag => ZigzagRuleChecker.Check(a),
        StructureKind.Flat => FlatRuleChecker.Check(a),
        StructureKind.Triangle => TriangleRuleChecker.Check(a),
        _ => ElliottRuleChecker.Check(a),
    };

    // Score a survivor with the shared guideline scorer; degrade to null on any shape it can't handle.
    private static double? ScoreOrNull(StructureKind kind, IReadOnlyList<WaveAnnotation> window)
    {
        try
        {
            var opts = new WaveScoringOptions();
            return kind switch
            {
                StructureKind.Impulse or StructureKind.Diagonal or StructureKind.Triangle =>
                    WaveGuidelineScorer.Score(kind, window, TerminalChildren(5), opts),
                StructureKind.Zigzag or StructureKind.Flat =>
                    WaveGuidelineScorer.Score(kind, window, TerminalChildren(3), opts),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static IReadOnlyList<StructureKind?> TerminalChildren(int waves)
        => Enumerable.Repeat<StructureKind?>(null, waves).ToList();

    private static string LabelFor(StructureKind kind, int index)
    {
        var scheme = kind switch
        {
            StructureKind.Zigzag or StructureKind.Flat => Corrective,
            StructureKind.Triangle => TriangleScheme,
            _ => Motive,
        };
        return index < scheme.Length ? scheme[index] : index.ToString();
    }

    private static readonly string[] Motive = ["0", "1", "2", "3", "4", "5"];
    private static readonly string[] Corrective = ["0", "A", "B", "C"];
    private static readonly string[] TriangleScheme = ["0", "A", "B", "C", "D", "E"];
}
