namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The candle timeframe an analysis is presented at. Both values are produced by resampling the
/// daily candles the providers fetch (see <see cref="Application.CandleResampler"/>), so no
/// intraday data source is required. A 4-hour timeframe needs an intraday-capable provider and
/// is a separate concern — deliberately not modelled here yet.
/// </summary>
public enum CandleInterval
{
    /// <summary>Daily candles as fetched (pass-through).</summary>
    OneDay,

    /// <summary>Weekly candles, aggregated from the daily series by ISO week.</summary>
    OneWeek,
}
