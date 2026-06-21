using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic checker for the three canonical Elliott Wave impulse rules plus the key
/// Fibonacci ratios. Pure math, no LLM — so the result is objective and reproducible. The
/// LLM consumes this as ground truth rather than re-deriving (and possibly hallucinating)
/// rule violations.
///
/// CONVENTION: the annotations, sorted by date, are read as consecutive wave pivots
/// P0, P1, P2, … where the move P[i-1] → P[i] is "wave i". A full impulse therefore needs
/// six pivots (origin + 1..5). When pivots for a rule are missing, that rule is reported as
/// <see cref="RuleStatus.Indeterminate"/> rather than guessed.
/// </summary>
public static class ElliottRuleChecker
{
    public static WaveRuleReport Check(IReadOnlyList<WaveAnnotation> annotations)
    {
        var prices = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        var bullish = prices.Count >= 2 && prices[1] > prices[0];

        var rules = new List<RuleResult>
        {
            CheckRule1(prices, bullish),
            CheckRule2(prices),
            CheckRule3(prices, bullish),
        };

        return new WaveRuleReport(bullish, rules, ComputeRatios(prices));
    }

    // Rule 1: Wave 2 never retraces beyond the start of Wave 1.
    private static RuleResult CheckRule1(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Rule 1 — Wave 2 stays within Wave 1's origin";
        if (p.Count < 3)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave 2.");
        }

        var ok = bullish ? p[2] > p[0] : p[2] < p[0];
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok ? "Wave 2 holds above the origin." : "Wave 2 retraced beyond the start of Wave 1.");
    }

    // Rule 2: Wave 3 is never the shortest of waves 1, 3, 5.
    private static RuleResult CheckRule2(IReadOnlyList<decimal> p)
    {
        const string name = "Rule 2 — Wave 3 is not the shortest impulse wave";
        if (p.Count < 6)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs all of waves 1, 3 and 5.");
        }

        var wave1 = Math.Abs(p[1] - p[0]);
        var wave3 = Math.Abs(p[3] - p[2]);
        var wave5 = Math.Abs(p[5] - p[4]);
        var shortest = wave3 < wave1 && wave3 < wave5;
        return new RuleResult(
            name,
            shortest ? RuleStatus.Fail : RuleStatus.Pass,
            shortest ? "Wave 3 is the shortest of waves 1/3/5." : "Wave 3 is not the shortest.");
    }

    // Rule 3: Wave 4 never overlaps the price territory of Wave 1.
    private static RuleResult CheckRule3(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Rule 3 — Wave 4 does not overlap Wave 1";
        if (p.Count < 5)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs pivots up to wave 4.");
        }

        var ok = bullish ? p[4] > p[1] : p[4] < p[1];
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok ? "Wave 4 stays clear of Wave 1." : "Wave 4 overlaps Wave 1's territory (only valid in diagonals).");
    }

    private static List<FibRatio> ComputeRatios(IReadOnlyList<decimal> p)
    {
        var ratios = new List<FibRatio>();

        if (p.Count >= 3 && p[1] != p[0])
        {
            ratios.Add(new FibRatio("Wave 2 retracement of Wave 1", Round(Math.Abs(p[2] - p[1]) / Math.Abs(p[1] - p[0]))));
        }
        if (p.Count >= 4 && p[1] != p[0])
        {
            ratios.Add(new FibRatio("Wave 3 extension of Wave 1", Round(Math.Abs(p[3] - p[2]) / Math.Abs(p[1] - p[0]))));
        }
        if (p.Count >= 5 && p[3] != p[2])
        {
            ratios.Add(new FibRatio("Wave 4 retracement of Wave 3", Round(Math.Abs(p[4] - p[3]) / Math.Abs(p[3] - p[2]))));
        }

        return ratios;
    }

    private static decimal Round(decimal value) => Math.Round(value, 3);
}
