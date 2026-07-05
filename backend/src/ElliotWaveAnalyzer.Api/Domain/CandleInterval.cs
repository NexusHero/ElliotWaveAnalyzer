namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The candle timeframe an analysis is presented at. Daily and weekly derive from the providers'
/// daily candles (weekly by resampling, see <see cref="Application.CandleResampler"/>). One-hour
/// comes from an intraday-capable provider (<see cref="Interfaces.IIntradayMarketDataProvider"/>)
/// and four-hour is resampled from those hourly bars. Intraday availability and lookback depth
/// depend on the data source and are reported honestly when exceeded
/// (<see cref="MarketDataRangeException"/>) — never silently substituted.
/// </summary>
public enum CandleInterval
{
    /// <summary>Daily candles as fetched (pass-through).</summary>
    OneDay,

    /// <summary>Weekly candles, aggregated from the daily series by ISO week.</summary>
    OneWeek,

    /// <summary>Hourly candles from an intraday-capable provider.</summary>
    OneHour,

    /// <summary>Four-hour candles, aggregated from hourly bars into UTC-aligned buckets.</summary>
    FourHours,
}
