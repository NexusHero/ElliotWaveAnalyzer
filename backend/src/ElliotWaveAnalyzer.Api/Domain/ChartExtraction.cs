namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The structured count a vision model extracted from an uploaded chart image: the (optional) symbol
/// and timeframe legible in the image, the claimed pivots with their labels, any drawn horizontal
/// levels, and any drawn price zones. Everything here is a <em>claim</em> from perception — it is
/// verified against real market data downstream, never trusted as-is.
/// </summary>
/// <param name="Symbol">Symbol read from the image, if any.</param>
/// <param name="Timeframe">Timeframe read from the image, if any.</param>
/// <param name="Pivots">The claimed labelled pivots.</param>
/// <param name="Levels">Claimed horizontal price levels.</param>
/// <param name="Zones">Claimed price zones/boxes.</param>
public sealed record ChartExtraction(
    string? Symbol,
    string? Timeframe,
    IReadOnlyList<ClaimedPivot> Pivots,
    IReadOnlyList<decimal> Levels,
    IReadOnlyList<ClaimedZone> Zones);
