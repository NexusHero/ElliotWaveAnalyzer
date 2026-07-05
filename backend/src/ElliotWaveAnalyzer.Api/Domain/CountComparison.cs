namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Side-by-side of the analyst's claimed count and our own best deterministic count on the same
/// window: the structures, their guideline scores, our scored confluence zones, and whether the two
/// agree on the structure kind.
/// </summary>
/// <param name="ClaimedStructure">The claimed count's structure (inferred from its labels).</param>
/// <param name="ClaimedScore">Guideline score of the claimed (snapped) count, if computable.</param>
/// <param name="OurStructure">Our best count's structure, or null if we found none.</param>
/// <param name="OurScore">Our best count's guideline score, if any.</param>
/// <param name="OurZones">Our scored confluence zones on this window.</param>
/// <param name="Agree">True when both name the same structure kind.</param>
/// <param name="Summary">A one-line human summary of the comparison.</param>
public sealed record CountComparison(
    string ClaimedStructure,
    decimal? ClaimedScore,
    string? OurStructure,
    decimal? OurScore,
    IReadOnlyList<ConfluenceZone> OurZones,
    bool Agree,
    string Summary);
