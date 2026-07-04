using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic checker for a zigzag correction (A-B-C, internally 5-3-5). Pure math, no
/// LLM — same contract as <see cref="ElliottRuleChecker"/>.
///
/// CONVENTION: annotations sorted by date are read as P0 (origin, where A begins), then the
/// terminals of A, B and C. "Bullish" means the structure itself travels up (A's leg is up);
/// a zigzag correcting a bull impulse is therefore bearish here.
/// </summary>
public static class ZigzagRuleChecker
{
    public static WaveRuleReport Check(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        var bullish = p.Count >= 2 && p[1] > p[0];

        var rules = new List<RuleResult> { CheckBHoldsOrigin(p, bullish), CheckCBeyondA(p, bullish) };
        return new WaveRuleReport(bullish, rules, ComputeRatios(p));
    }

    // Hard rule: B never retraces beyond the start of A (that would unwind the correction).
    private static RuleResult CheckBHoldsOrigin(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Zigzag — Wave B stays within Wave A's origin";
        if (p.Count < 3)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave B.");
        }

        var ok = bullish ? p[2] > p[0] : p[2] < p[0];
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok ? "Wave B holds inside Wave A's origin." : "Wave B retraced beyond the start of Wave A.");
    }

    // Guideline: C normally travels beyond the end of A; a truncated C is rare but valid.
    private static RuleResult CheckCBeyondA(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Zigzag guideline — Wave C extends beyond Wave A's end";
        if (p.Count < 4)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave C.")
            { IsGuideline = true };
        }

        var ok = bullish ? p[3] > p[1] : p[3] < p[1];
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok ? "Wave C travels beyond Wave A." : "Truncated C — Wave C stopped short of Wave A's end (rare).")
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
