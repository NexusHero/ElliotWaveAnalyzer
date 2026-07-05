using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// A market-data source that can serve <b>hourly</b> candles (the basis for 1H and, after
/// resampling, 4H analysis). Separate from <see cref="IMarketDataProvider"/> (ISP): a source that
/// only offers daily data does not implement this, so callers never assume intraday support that
/// isn't there. Four-hour bars are derived from the hourly series via
/// <see cref="Application.CandleResampler"/> — the provider only returns hourly.
/// </summary>
public interface IIntradayMarketDataProvider
{
    /// <summary>True if this source can serve hourly candles for <paramref name="symbol"/>.</summary>
    bool SupportsIntraday(string symbol);

    /// <summary>
    /// Retrieves hourly OHLCV candles covering the last <paramref name="days"/> days, ascending by
    /// time. Throws <see cref="MarketDataRangeException"/> when <paramref name="days"/> exceeds the
    /// source's hourly lookback limit — the caller must surface the limit, not silently truncate.
    /// </summary>
    Task<IReadOnlyList<MarketCandle>> GetHourlyCandlesAsync(
        string symbol, int days, CancellationToken cancellationToken = default);
}
