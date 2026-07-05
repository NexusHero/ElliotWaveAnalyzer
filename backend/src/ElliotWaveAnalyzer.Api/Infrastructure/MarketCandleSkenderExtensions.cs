using ElliotWaveAnalyzer.Api.Domain;
using Skender.Stock.Indicators;

namespace ElliotWaveAnalyzer.Api.Infrastructure;

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
