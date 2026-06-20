namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// MACD values for a single date. All components are null during the warm-up period
/// (first <c>slowPeriods</c> candles before the slow EMA has enough data).
/// <para>
/// MacdLine = FastEMA - SlowEMA (default: 12-period EMA minus 26-period EMA).
/// SignalLine = EMA of MacdLine (default: 9-period EMA of MacdLine).
/// Histogram = MacdLine - SignalLine.
/// </para>
/// </summary>
public sealed record MacdResult(
    DateTime Date,
    decimal? MacdLine,
    decimal? SignalLine,
    decimal? Histogram);
