using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Pure calculation contract: takes candles, returns indicator series.
/// Stateless — implementations must not cache or hold mutable state.
/// <para>
/// The concrete implementation (<c>SkenderIndicatorCalculator</c>) is the only
/// place that knows about Skender.Stock.Indicators. All callers depend on this
/// interface only (Dependency Inversion).
/// </para>
/// </summary>
public interface IIndicatorCalculator
{
    /// <summary>
    /// Calculates RSI using Wilder's Smoothing Method.
    /// Returns one entry per input candle; entries within the warm-up period have <c>Value = null</c>.
    /// </summary>
    IReadOnlyList<RsiResult> CalculateRsi(IReadOnlyList<MarketCandle> candles, int period = 14);

    /// <summary>
    /// Calculates MACD (Moving Average Convergence/Divergence).
    /// Returns one entry per input candle; warm-up entries have null components.
    /// </summary>
    IReadOnlyList<MacdResult> CalculateMacd(
        IReadOnlyList<MarketCandle> candles,
        int fastPeriods = 12,
        int slowPeriods = 26,
        int signalPeriods = 9);

    /// <summary>
    /// Calculates the Wilder-smoothed Average True Range (volatility).
    /// Returns one entry per input candle; entries within the warm-up period have <c>Value = null</c>.
    /// </summary>
    IReadOnlyList<AtrResult> CalculateAtr(IReadOnlyList<MarketCandle> candles, int period = 14);
}
