namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Average True Range for one candle. <see cref="Value"/> is null during the warm-up window
/// (the first <c>period</c> candles), matching the RSI/MACD result convention.
/// </summary>
public sealed record AtrResult(DateTime Date, decimal? Value);
