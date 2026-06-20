namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A single OHLCV candle. Immutable value object — records enforce this.
/// <para>
/// Volume may be 0 for providers that don't expose it (e.g. CoinGecko free-tier OHLC endpoint).
/// RSI and MACD only use the Close price, so Volume=0 is safe for indicator calculation.
/// </para>
/// </summary>
public sealed record MarketCandle(
    DateTime OpenTime,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    decimal Volume);
