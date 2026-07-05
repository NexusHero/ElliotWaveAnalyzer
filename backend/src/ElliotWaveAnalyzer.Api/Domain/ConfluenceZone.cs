namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A price band where several Fibonacci relationships cluster — the "green box" a human analyst
/// draws. <see cref="Score"/> reflects how many levels (weighted by degree) fall inside it, so a
/// zone where multiple degrees agree scores higher than a lone level. <see cref="Contributions"/>
/// lists exactly which levels produced it, each labelled.
/// </summary>
/// <param name="Low">Lower price bound.</param>
/// <param name="High">Upper price bound.</param>
/// <param name="Score">Confluence strength (sum of contributing weights); higher = stronger.</param>
/// <param name="Kind">Entry (pullback) or target zone.</param>
/// <param name="Scale">The price scale the levels were computed on.</param>
/// <param name="Contributions">The Fibonacci levels that fall in this zone, each labelled.</param>
public sealed record ConfluenceZone(
    decimal Low,
    decimal High,
    decimal Score,
    ZoneKind Kind,
    FibScale Scale,
    IReadOnlyList<ContributingLevel> Contributions);
