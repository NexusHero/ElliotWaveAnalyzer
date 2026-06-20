namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// RSI value for a single date. Value is null for the warm-up period
/// (first <c>period</c> candles) before Wilder's Smoothing stabilizes.
/// </summary>
public sealed record RsiResult(DateTime Date, decimal? Value);
