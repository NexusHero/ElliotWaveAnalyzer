namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Input to the pure top-down analyzer: one timeframe's label and its detected swing pivots.
/// The analyzer takes these already-computed (no I/O), so it stays pure and unit-testable; the
/// service layer is responsible for fetching candles and detecting pivots per timeframe.
/// </summary>
/// <param name="Interval">Timeframe label, e.g. "1W", "1D", "4H".</param>
/// <param name="Pivots">Alternating swing pivots for the timeframe, chronological.</param>
public sealed record TimeframePivots(string Interval, IReadOnlyList<SwingPivot> Pivots);
