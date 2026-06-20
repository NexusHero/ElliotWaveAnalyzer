using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Skender.Stock.Indicators;

// Alias to resolve the name conflict between our domain MacdResult
// and Skender.Stock.Indicators.MacdResult within this file.
using SkenderMacd = Skender.Stock.Indicators.MacdResult;
using SkenderRsi = Skender.Stock.Indicators.RsiResult;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

/// <summary>
/// Calculates RSI and MACD using Skender.Stock.Indicators.
///
/// WHY Skender and not a custom implementation:
/// RSI uses Wilder's Smoothing (exponential, not simple moving average) with a
/// specific warm-up convention. MACD requires EMA seeded correctly. Both have
/// subtle edge cases that are easy to get wrong and hard to catch without
/// exhaustive test data. Skender is open-source, well-tested, and benchmarked.
///
/// ISOLATION: Skender types are contained entirely within this class.
/// The Skender IQuote interface is bridged via a private adapter —
/// no Skender type leaks into the domain or application layers.
/// </summary>
public sealed class SkenderIndicatorCalculator : IIndicatorCalculator
{
    /// <inheritdoc/>
    public IReadOnlyList<Domain.RsiResult> CalculateRsi(
        IReadOnlyList<MarketCandle> candles, int period = 14)
    {
        return candles
            .AsSkenderQuotes()
            .GetRsi(period)
            .Select(r => new Domain.RsiResult(
                Date: r.Date,
                Value: r.Rsi.HasValue ? (decimal)r.Rsi.Value : null))
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<Domain.MacdResult> CalculateMacd(
        IReadOnlyList<MarketCandle> candles,
        int fastPeriods = 12,
        int slowPeriods = 26,
        int signalPeriods = 9)
    {
        return candles
            .AsSkenderQuotes()
            .GetMacd(fastPeriods, slowPeriods, signalPeriods)
            .Select(r => new Domain.MacdResult(
                Date: r.Date,
                MacdLine: r.Macd.HasValue ? (decimal)r.Macd.Value : null,
                SignalLine: r.Signal.HasValue ? (decimal)r.Signal.Value : null,
                Histogram: r.Histogram.HasValue ? (decimal)r.Histogram.Value : null))
            .ToList();
    }
}

/// <summary>
/// Internal extension that converts domain <see cref="MarketCandle"/> to Skender's
/// <see cref="IQuote"/> without coupling the domain to third-party contracts.
/// </summary>
internal static class MarketCandleSkenderExtensions
{
    internal static IEnumerable<IQuote> AsSkenderQuotes(this IReadOnlyList<MarketCandle> candles)
        => candles.Select(c => new SkenderQuoteAdapter(c));

    /// <summary>
    /// Adapter (GoF): bridges <see cref="MarketCandle"/> to <see cref="IQuote"/>.
    /// Private so nothing outside this file can reference the Skender-coupled type.
    /// </summary>
    private sealed class SkenderQuoteAdapter(MarketCandle candle) : IQuote
    {
        public DateTime Date => candle.OpenTime;
        public decimal Open => candle.Open;
        public decimal High => candle.High;
        public decimal Low => candle.Low;
        public decimal Close => candle.Close;
        public decimal Volume => candle.Volume;
    }
}
