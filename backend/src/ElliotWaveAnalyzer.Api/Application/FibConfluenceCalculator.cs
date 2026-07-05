using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Turns Fibonacci relationships from one or more <see cref="FibLeg"/>s into scored
/// <see cref="ConfluenceZone"/>s — the "green boxes" where several ratios (ideally from different
/// degrees) cluster. Pure and deterministic: generate candidate levels, sort by price, greedily
/// group levels within a tolerance band, and score each group by the sum of its contributors'
/// degree weights (so more levels — and higher degrees — mean a stronger zone). Zones are returned
/// strongest first.
/// </summary>
public static class FibConfluenceCalculator
{
    private static readonly decimal[] RetracementRatios = [0.382m, 0.5m, 0.618m, 0.786m];
    private static readonly decimal[] ExtensionRatios = [1.0m, 1.272m, 1.618m];

    /// <summary>
    /// Entry zones for an expected pullback: retracements of each leg, clustered. Use for the wave
    /// 2/4/B the count is waiting on.
    /// </summary>
    public static IReadOnlyList<ConfluenceZone> EntryZones(
        IReadOnlyList<FibLeg> legs, FibScale scale, decimal tolerancePercent = 1.0m)
    {
        var levels =
            from leg in legs
            from ratio in RetracementRatios
            select new ContributingLevel(
                FibMath.Retrace(leg.Start, leg.End, ratio, scale),
                leg.DegreeWeight,
                $"{ratio * 100m:0.#}% retracement of {leg.Label}{ScaleSuffix(scale)}");

        return Cluster([.. levels], ZoneKind.Entry, scale, tolerancePercent);
    }

    /// <summary>
    /// Target zones for an expected motive move: extensions of each leg projected from
    /// <paramref name="projectFrom"/> (typically the pullback's end). Use for wave 3/5/C.
    /// </summary>
    public static IReadOnlyList<ConfluenceZone> TargetZones(
        IReadOnlyList<FibLeg> legs, decimal projectFrom, FibScale scale, decimal tolerancePercent = 1.0m)
    {
        var levels =
            from leg in legs
            from multiple in ExtensionRatios
            select new ContributingLevel(
                FibMath.Extend(projectFrom, leg.Start, leg.End, multiple, scale),
                leg.DegreeWeight,
                $"{multiple:0.###}× extension of {leg.Label}{ScaleSuffix(scale)}");

        return Cluster([.. levels], ZoneKind.Target, scale, tolerancePercent);
    }

    private static IReadOnlyList<ConfluenceZone> Cluster(
        List<ContributingLevel> levels, ZoneKind kind, FibScale scale, decimal tolerancePercent)
    {
        if (levels.Count == 0)
        {
            return [];
        }

        var ordered = levels.OrderBy(l => l.Price).ToList();
        var zones = new List<ConfluenceZone>();
        var group = new List<ContributingLevel> { ordered[0] };

        for (var i = 1; i < ordered.Count; i++)
        {
            var anchor = group[0].Price;
            var within = anchor != 0m
                && Math.Abs((ordered[i].Price - anchor) / anchor) * 100m <= tolerancePercent;

            if (within)
            {
                group.Add(ordered[i]);
            }
            else
            {
                zones.Add(ToZone(group, kind, scale));
                group = [ordered[i]];
            }
        }

        zones.Add(ToZone(group, kind, scale));

        // Strongest first; ties broken by price for determinism.
        return [.. zones.OrderByDescending(z => z.Score).ThenBy(z => z.Low)];
    }

    private static ConfluenceZone ToZone(
        IReadOnlyList<ContributingLevel> group, ZoneKind kind, FibScale scale)
        => new(
            Low: group.Min(l => l.Price),
            High: group.Max(l => l.Price),
            Score: group.Sum(l => l.Weight),
            Kind: kind,
            Scale: scale,
            Contributions: [.. group.OrderBy(l => l.Price)]);

    private static string ScaleSuffix(FibScale scale) => scale == FibScale.Log ? ", log scale" : ", linear scale";
}
