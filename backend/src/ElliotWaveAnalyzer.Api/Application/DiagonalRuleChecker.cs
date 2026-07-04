using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic checker for a contracting diagonal (five-wave motive wedge). Diagonals keep
/// two of the three impulse rules (wave 2 holds the origin, wave 3 is never the shortest) but
/// explicitly ALLOW wave 4 to overlap wave 1 — which is why running impulse rules against a
/// diagonal produces a false violation. The wedge itself is enforced by contraction:
/// wave 5 &lt; wave 3 &lt; wave 1 and wave 4 &lt; wave 2, which makes the 1-3 and 2-4 boundary
/// lines converge. Leading vs. ending is contextual (position in the parent structure), not
/// geometric, so it is not decided here.
///
/// CONVENTION (matches <see cref="ElliottRuleChecker"/>): annotations sorted by date are read
/// as consecutive pivots P0, P1, … where the move P[i-1] → P[i] is "wave i".
/// </summary>
public static class DiagonalRuleChecker
{
    public static WaveRuleReport Check(IReadOnlyList<WaveAnnotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(annotations);
        var p = annotations.OrderBy(a => a.Date).Select(a => a.Price).ToList();
        var bullish = p.Count >= 2 && p[1] > p[0];

        var rules = new List<RuleResult>
        {
            CheckWave2HoldsOrigin(p, bullish),
            CheckWave3NotShortest(p),
            CheckWedgeContraction(p),
        };

        return new WaveRuleReport(bullish, rules, ComputeRatios(p));
    }

    // Hard rule (shared with impulses): wave 2 never retraces beyond the start of wave 1.
    private static RuleResult CheckWave2HoldsOrigin(IReadOnlyList<decimal> p, bool bullish)
    {
        const string name = "Diagonal — Wave 2 stays within Wave 1's origin";
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

    // Hard rule (shared with impulses): wave 3 is never the shortest of 1, 3, 5.
    private static RuleResult CheckWave3NotShortest(IReadOnlyList<decimal> p)
    {
        const string name = "Diagonal — Wave 3 is not the shortest impulse wave";
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

    // Hard rule: the wedge. Contracting spans are what make the boundary lines converge —
    // a deterministic proxy that avoids fitting actual trendlines through dated points.
    private static RuleResult CheckWedgeContraction(IReadOnlyList<decimal> p)
    {
        const string name = "Diagonal — legs contract into a wedge (5 < 3 < 1 and 4 < 2)";
        if (p.Count < 6)
        {
            return new RuleResult(name, RuleStatus.Indeterminate, "Needs all five waves.");
        }

        var wave1 = Math.Abs(p[1] - p[0]);
        var wave2 = Math.Abs(p[2] - p[1]);
        var wave3 = Math.Abs(p[3] - p[2]);
        var wave4 = Math.Abs(p[4] - p[3]);
        var wave5 = Math.Abs(p[5] - p[4]);

        var ok = wave5 < wave3 && wave3 < wave1 && wave4 < wave2;
        return new RuleResult(
            name,
            ok ? RuleStatus.Pass : RuleStatus.Fail,
            ok
                ? "Motive and corrective legs both contract — the wedge converges."
                : "Legs do not contract — no wedge (an expanding diagonal is out of scope).");
    }

    private static List<FibRatio> ComputeRatios(IReadOnlyList<decimal> p)
    {
        var ratios = new List<FibRatio>();

        if (p.Count >= 4 && p[1] != p[0])
        {
            ratios.Add(new FibRatio(
                "Wave 3 / Wave 1", Round(Math.Abs(p[3] - p[2]) / Math.Abs(p[1] - p[0]))));
        }
        if (p.Count >= 6 && p[3] != p[2])
        {
            ratios.Add(new FibRatio(
                "Wave 5 / Wave 3", Round(Math.Abs(p[5] - p[4]) / Math.Abs(p[3] - p[2]))));
        }

        return ratios;
    }

    private static decimal Round(decimal value) => Math.Round(value, 3);
}
