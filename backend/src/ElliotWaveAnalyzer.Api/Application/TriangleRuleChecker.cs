using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic checker for a contracting triangle (A-B-C-D-E, each leg a three). The
/// defining geometry is contraction: every same-direction leg is shorter than the one two
/// steps before it, which is what makes the boundary lines converge toward an apex.
/// Barrier and expanding variants are deliberately out of scope for now.
///
/// CONVENTION: annotations sorted by date are read as P0 (origin, where A begins), then the
/// terminals of A..E — legs are P0→A, A→B, B→C, C→D, D→E. "Bullish" means leg A travels up.
/// </summary>
public static class TriangleRuleChecker
{
    public static WaveRuleReport Check(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        var bullish = p.Count >= 2 && p[1] > p[0];

        var rules = new List<RuleResult>
        {
            CheckContraction(p, legIdx: 3, priorLegIdx: 1, "Wave C is shorter than Wave A"),
            CheckContraction(p, legIdx: 4, priorLegIdx: 2, "Wave D is shorter than Wave B"),
            CheckContraction(p, legIdx: 5, priorLegIdx: 3, "Wave E is shorter than Wave C"),
        };

        return new WaveRuleReport(bullish, rules, ComputeRatios(p));
    }

    // Hard rule: each same-direction leg contracts. Leg i spans p[i-1] → p[i].
    private static RuleResult CheckContraction(
        IReadOnlyList<decimal> p, int legIdx, int priorLegIdx, string description)
    {
        var name = $"Triangle — {description}";
        if (p.Count <= legIdx)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, $"Needs pivots up to leg {legIdx}.");
        }

        var leg = Math.Abs(p[legIdx] - p[legIdx - 1]);
        var prior = Math.Abs(p[priorLegIdx] - p[priorLegIdx - 1]);
        var ok = leg < prior;
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok ? "Leg contracts as required." : "Leg does not contract — boundary lines cannot converge.");
    }

    private static List<FibRatio> ComputeRatios(IReadOnlyList<decimal> p)
    {
        var ratios = new List<FibRatio>();
        AddLegRatio(ratios, p, 3, 1, "Wave C / Wave A");
        AddLegRatio(ratios, p, 4, 2, "Wave D / Wave B");
        AddLegRatio(ratios, p, 5, 3, "Wave E / Wave C");
        return ratios;
    }

    private static void AddLegRatio(
        List<FibRatio> ratios, IReadOnlyList<decimal> p, int legIdx, int priorLegIdx, string name)
    {
        if (p.Count <= legIdx)
        {
            return;
        }

        var prior = Math.Abs(p[priorLegIdx] - p[priorLegIdx - 1]);
        if (prior == 0)
        {
            return;
        }

        ratios.Add(new FibRatio(name, Math.Round(Math.Abs(p[legIdx] - p[legIdx - 1]) / prior, 3)));
    }
}
