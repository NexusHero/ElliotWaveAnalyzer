namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A depot position that could not be reviewed, with a clear reason (e.g. no market-data source for
/// the ISIN, or no timeframe could be analyzed). Surfaced explicitly so an unreviewable holding is
/// never a silent gap in the portfolio review.
/// </summary>
/// <param name="Isin">The position's ISIN.</param>
/// <param name="Name">The position's name as it appears in the depot.</param>
/// <param name="Reason">Why it could not be reviewed.</param>
public sealed record UnresolvedPosition(string Isin, string Name, string Reason);
