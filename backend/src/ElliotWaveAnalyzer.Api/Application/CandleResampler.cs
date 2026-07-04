using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Aggregates daily candles into a coarser timeframe. Daily is a pass-through; weekly groups the
/// daily candles by ISO week (Monday-based) and OHLCV-aggregates each group: open = first candle's
/// open, high = max high, low = min low, close = last candle's close, volume = sum. The result is
/// ordered by time and each bar is stamped with the first (earliest) day in its group.
///
/// WHY resample instead of asking the provider: the free data sources don't offer a clean weekly
/// feed, but weekly is an exact, deterministic function of the daily candles we already fetch — so
/// it lives here as pure, exhaustively-testable math with no extra I/O or provider coupling.
/// </summary>
public static class CandleResampler
{
    /// <summary>Resamples <paramref name="daily"/> to <paramref name="interval"/>.</summary>
    public static IReadOnlyList<MarketCandle> Resample(
        IReadOnlyList<MarketCandle> daily, CandleInterval interval)
    {
        ArgumentNullException.ThrowIfNull(daily);

        return interval switch
        {
            CandleInterval.OneWeek => ToWeekly(daily),
            _ => daily, // OneDay: pass-through
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
                return new MarketCandle(
                    OpenTime: bars[0].OpenTime,
                    Open: bars[0].Open,
                    High: bars.Max(b => b.High),
                    Low: bars.Min(b => b.Low),
                    Close: bars[^1].Close,
                    Volume: bars.Sum(b => b.Volume));
            })
            .ToList();
    }

    /// <summary>ISO-week identity (year + week number), so weeks are unique and chronologically sortable.</summary>
    private static (int Year, int Week) WeekKey(DateTime date)
        => (ISOWeek.GetYear(date), ISOWeek.GetWeekOfYear(date));
}
