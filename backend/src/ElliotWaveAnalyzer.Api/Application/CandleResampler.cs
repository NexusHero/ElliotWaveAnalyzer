using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Aggregates candles into a coarser timeframe. OHLCV aggregation is always the same: open = first
/// bar's open, high = max high, low = min low, close = last bar's close, volume = sum. Only the
/// grouping differs by target interval:
/// <list type="bullet">
///   <item><b>Weekly</b> groups the daily series by ISO week (Monday-based); each weekly bar is
///   stamped with the first (earliest) day in the group.</item>
///   <item><b>Four-hour</b> groups the hourly series into UTC-aligned 4-hour buckets
///   (00–04, 04–08, …); each bar is stamped with the bucket start, so bars align regardless of
///   gaps.</item>
///   <item><b>Daily / One-hour</b> are a pass-through (the providers already return them).</item>
/// </list>
///
/// WHY resample instead of asking the provider: weekly and 4H are exact, deterministic functions of
/// data we already fetch (daily / hourly), so they live here as pure, exhaustively-testable math
/// with no extra I/O or provider coupling.
/// </summary>
public static class CandleResampler
{
    /// <summary>Resamples <paramref name="source"/> to <paramref name="interval"/>.</summary>
    public static IReadOnlyList<MarketCandle> Resample(
        IReadOnlyList<MarketCandle> source, CandleInterval interval)
    {
        ArgumentNullException.ThrowIfNull(source);

        return interval switch
        {
            CandleInterval.OneWeek => ToWeekly(source),
            CandleInterval.FourHours => ToFourHour(source),
            _ => source, // OneDay / OneHour: pass-through
        };
    }

    private static IReadOnlyList<MarketCandle> ToWeekly(IReadOnlyList<MarketCandle> daily)
    {
        if (daily.Count == 0)
        {
            return daily;
        }

        return daily
            .OrderBy(c => c.OpenTime)
            .GroupBy(c => WeekKey(c.OpenTime))
            .OrderBy(g => g.Key)
            .Select(week =>
            {
                var bars = week.ToList(); // already time-ordered within the group
                return Aggregate(bars[0].OpenTime, bars);
            })
            .ToList();
    }

    private static IReadOnlyList<MarketCandle> ToFourHour(IReadOnlyList<MarketCandle> hourly)
    {
        if (hourly.Count == 0)
        {
            return hourly;
        }

        return hourly
            .OrderBy(c => c.OpenTime)
            .GroupBy(c => FourHourBucket(c.OpenTime))
            .OrderBy(g => g.Key)
            .Select(bucket => Aggregate(bucket.Key, bucket.ToList()))
            .ToList();
    }

    /// <summary>OHLCV aggregation of a time-ordered group, stamped with <paramref name="openTime"/>.</summary>
    private static MarketCandle Aggregate(DateTime openTime, IReadOnlyList<MarketCandle> bars)
        => new(
            OpenTime: openTime,
            Open: bars[0].Open,
            High: bars.Max(b => b.High),
            Low: bars.Min(b => b.Low),
            Close: bars[^1].Close,
            Volume: bars.Sum(b => b.Volume));

    /// <summary>ISO-week identity (year + week number), unique and chronologically sortable.</summary>
    private static (int Year, int Week) WeekKey(DateTime date)
        => (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));

    /// <summary>UTC-aligned 4-hour bucket start (00, 04, 08, 12, 16, 20).</summary>
    private static DateTime FourHourBucket(DateTime time)
        => new(time.Year, time.Month, time.Day, time.Hour / 4 * 4, 0, 0, DateTimeKind.Utc);
}
