using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic checker for a flat correction (A-B-C, internally 3-3-5). What separates a
/// flat from a zigzag is the deep B: at least ~90% of A. Pure math, no LLM — same contract
/// as <see cref="ElliottRuleChecker"/>.
///
/// CONVENTION: annotations sorted by date are read as P0 (origin), then the terminals of
/// A, B and C. "Bullish" means the structure itself travels up.
/// </summary>
public static class FlatRuleChecker
{
    /// <summary>B must reach at least this fraction of A for the structure to be a flat.</summary>
    private const decimal MinBRetrace = 0.9m;

    /// <summary>Above this fraction B is considered beyond A's origin (expanded/running).</summary>
    private const decimal ExpandedBRetrace = 1.05m;

    public static WaveRuleReport Check(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        var bullish = p.Count >= 2 && p[1] > p[0];

        var rules = new List<RuleResult> { CheckDeepB(p), CheckCBeyondA(p, bullish) };
        return new WaveRuleReport(bullish, rules, ComputeRatios(p));
    }

    /// <summary>
    /// Classifies the flat variant (regular / expanded / running) from B's depth and where C
    /// ends. Only meaningful when <see cref="Check"/> reports no hard failure; returns null
    /// while pivots up to C are missing.
    /// </summary>
    public static FlatVariant? Classify(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        if (p.Count < 4 || p[1] == p[0])
        {
            return null;
        }

        var bullish = p[1] > p[0];
        var bRetrace = Math.Abs(p[2] - p[1]) / Math.Abs(p[1] - p[0]);
        var cBeyondA = bullish ? p[3] > p[1] : p[3] < p[1];

        if (!cBeyondA)
        {
            return FlatVariant.Running;
        }

        return bRetrace > ExpandedBRetrace ? FlatVariant.Expanded : FlatVariant.Regular;
    }

    // Hard rule: the deep B is what makes it a flat. Below ~90% the pattern is a zigzag.
    private static RuleResult CheckDeepB(IReadOnlyList<decimal> p)
    {
        const string name = "Flat — Wave B retraces at least 90% of Wave A";
        if (p.Count < 3)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave B.");
        }
        if (p[1] == p[0])
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Wave A has zero length.");
        }

        var retrace = Math.Abs(p[2] - p[1]) / Math.Abs(p[1] - p[0]);
        var ok = retrace >= MinBRetrace;
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok
                ? $"Wave B retraced {retrace:P0} of Wave A."
                : $"Wave B only retraced {retrace:P0} of Wave A — too shallow for a flat (zigzag territory).");
    }

    // Guideline: C normally ends beyond A; if it falls short the flat is "running".
    private static RuleResult CheckCBeyondA(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Flat guideline — Wave C ends beyond Wave A's end";
        if (p.Count < 4)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave C.")
            { IsGuideline = true };
        }

        var ok = bullish ? p[3] > p[1] : p[3] < p[1];
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok
                ? "Wave C travels beyond Wave A (regular/expanded flat)."
                : "Wave C stopped short of Wave A's end — a running flat (signals underlying strength).")
        { IsGuideline = true };
    }

    private static List<FibRatio> ComputeRatios(IReadOnlyList<decimal> p)
    {
        var ratios = new List<FibRatio>();

        if (p.Count >= 3 && p[1] != p[0])
        {
            ratios.Add(new FibRatio(
                "Wave B retracement of Wave A",
                Round(Math.Abs(p[2] - p[1]) / Math.Abs(p[1] - p[0]))));
        }
        if (p.Count >= 4 && p[1] != p[0])
        {
            ratios.Add(new FibRatio(
                "Wave C extension of Wave A",
                Round(Math.Abs(p[3] - p[2]) / Math.Abs(p[1] - p[0]))));
        }

        return ratios;
    }

    private static decimal Round(decimal value) => Math.Round(value, 3);
}
