using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Builds the deterministic verification report from a vision extraction and real candles: snap the
/// claimed pivots (hallucination guard), and only if enough survive, run the hard rules on the snapped
/// count and compare it side-by-side with our own best parse of the same window. All judgment is
/// deterministic — the vision model perceived, the rules decide (ADR-009). Pure and static.
/// </summary>
public static class ChartVerificationAssembler
{
    /// <summary>Pivot threshold for detecting our own count on the window.</summary>
    private const decimal OurPivotThresholdPercent = 3m;

    /// <summary>Assembles the report for <paramref name="extraction"/> against <paramref name="candles"/>.</summary>
    public static ImageVerificationReport Assemble(
        ChartExtraction extraction, IReadOnlyList<MarketCandle> candles)
    {
        ArgumentNullException.ThrowIfNull(extraction);
        ArgumentNullException.ThrowIfNull(candles);

        var (snapped, rejected) = PivotSnapper.Snap(extraction.Pivots, candles);
        var claimedStructure = InferStructure(extraction.Pivots.Select(p => p.Label));
        var required = RequiredPivots(claimedStructure);

        if (snapped.Count < required)
        {
            return new ImageVerificationReport(
                ImageVerificationStatus.ExtractionUnreliable, extraction, snapped, rejected,
                ClaimedRules: null, Comparison: null,
                Message: $"The image could not be reliably extracted: only {snapped.Count} of the " +
                    $"{required} pivots a {claimedStructure.ToLowerInvariant()} needs snapped to real candles.");
        }

        var claimedAnnotations = snapped
            .OrderBy(s => s.Date)
            .Select(s => new WaveAnnotation(s.Date, s.Price, s.Label))
            .ToList();
        var claimedRules = ElliottRuleChecker.Check(claimedAnnotations);

        var comparison = CompareWithOurCount(claimedStructure, candles);

        var violated = claimedRules.Rules.Count(r => r is { Status: RuleStatus.Fail, IsGuideline: false });
        var verdict = violated == 0 ? "passes the hard rules" : $"violates {violated} hard rule(s)";
        return new ImageVerificationReport(
            ImageVerificationStatus.Verified, extraction, snapped, rejected, claimedRules, comparison,
            Message: $"Extracted a {claimedStructure.ToLowerInvariant()} count that {verdict}; " +
                $"{comparison.Summary}");
    }

    private static CountComparison CompareWithOurCount(string claimedStructure, IReadOnlyList<MarketCandle> candles)
    {
        var pivots = SwingPivotDetector.Detect(candles, OurPivotThresholdPercent);
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(pivots);
        var best = candidates.Count > 0 ? candidates[0] : null;

        var ourStructure = best?.Structure;
        var ourZones = best?.Levels?.ConfluenceZones ?? [];
        var agree = ourStructure is not null && Family(ourStructure) == Family(claimedStructure);

        var summary = ourStructure is null
            ? "our engine found no rule-valid count on this window"
            : agree
                ? $"our engine agrees it is a {Family(ourStructure).ToLowerInvariant()} structure"
                : $"our engine reads a {ourStructure} instead";

        return new CountComparison(claimedStructure, ClaimedScore: null, ourStructure, best?.Score, ourZones, agree, summary);
    }

    /// <summary>Infers the claimed structure family from the drawn labels.</summary>
    private static string InferStructure(IEnumerable<string> labels)
    {
        var set = labels.Select(l => l.Trim().ToUpperInvariant()).ToHashSet();
        if (set.Contains("5") || set.Contains("4") || set.Contains("3"))
        {
            return "Impulse";
        }

        return set.Contains("C") || set.Contains("B") ? "Corrective" : "Unknown";
    }

    /// <summary>Pivots a claimed structure needs before it can be rule-checked (incl. the origin).</summary>
    private static int RequiredPivots(string structure) => structure switch
    {
        "Impulse" => 6,
        "Corrective" => 4,
        _ => 3,
    };

    /// <summary>Maps a concrete structure kind to its family so a claim and our count can be compared.</summary>
    private static string Family(string structure) => structure switch
    {
        "Impulse" or "Diagonal" => "Impulse",
        "Zigzag" or "Flat" or "Triangle" or "Corrective" => "Corrective",
        _ => structure,
    };
}
