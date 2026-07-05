using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="CandleResampler"/>: daily/hourly pass-through, weekly
/// OHLCV aggregation by ISO week, and 4-hour aggregation into UTC-aligned buckets.
/// </summary>
[TestFixture]
public sealed class CandleResamplerTests
{
    // 2024-01-01 is a Monday, so a clean ISO-week boundary.
    private static readonly DateTime Monday = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static MarketCandle Day(int offset, decimal open, decimal high, decimal low, decimal close, decimal vol = 0m)
        => new(Monday.AddDays(offset), open, high, low, close, vol);

    private static MarketCandle Hour(int offset, decimal open, decimal high, decimal low, decimal close, decimal vol = 0m)
        => new(Monday.AddHours(offset), open, high, low, close, vol);

    [Test]
    public void Daily_IsPassThrough()
    {
        var daily = new[] { Day(0, 100, 110, 95, 105), Day(1, 105, 115, 100, 112) };

        var result = CandleResampler.Resample(daily, CandleInterval.OneDay);

        Assert.That(result, Is.SameAs(daily));
    }

    [Test]
    public void Empty_ReturnsEmpty()
    {
        Assert.That(CandleResampler.Resample([], CandleInterval.OneWeek), Is.Empty);
    }

    [Test]
    public void Weekly_AggregatesOhlcvWithinAWeek()
    {
        // Mon–Wed of the same ISO week.
        var daily = new[]
        {
            Day(0, 100m, 120m, 90m, 110m, 10m),
            Day(1, 110m, 130m, 105m, 115m, 20m),
            Day(2, 115m, 118m, 80m, 95m, 30m),
        };

        var weekly = CandleResampler.Resample(daily, CandleInterval.OneWeek);

        Assert.That(weekly, Has.Count.EqualTo(1));
        var w = weekly[0];
        Assert.Multiple(() =>
        {
            Assert.That(w.OpenTime, Is.EqualTo(Monday));       // first day of the group
            Assert.That(w.Open, Is.EqualTo(100m));             // first open
            Assert.That(w.High, Is.EqualTo(130m));             // max high
            Assert.That(w.Low, Is.EqualTo(80m));               // min low
            Assert.That(w.Close, Is.EqualTo(95m));             // last close
            Assert.That(w.Volume, Is.EqualTo(60m));            // summed volume
        });
    }

    [Test]
    public void Weekly_SplitsAcrossIsoWeekBoundaries()
    {
        // Sunday (day 6) ends week 1; Monday (day 7) starts week 2.
        var daily = new[]
        {
            Day(5, 100m, 110m, 95m, 105m),  // Sat, week 1
            Day(6, 105m, 112m, 100m, 108m), // Sun, week 1
            Day(7, 108m, 120m, 104m, 118m), // Mon, week 2
        };

        var weekly = CandleResampler.Resample(daily, CandleInterval.OneWeek);

        Assert.Multiple(() =>
        {
            Assert.That(weekly, Has.Count.EqualTo(2));
            Assert.That(weekly[0].Close, Is.EqualTo(108m)); // Sunday close ends week 1
            Assert.That(weekly[1].Open, Is.EqualTo(108m));  // Monday opens week 2
            Assert.That(weekly[1].High, Is.EqualTo(120m));
        });
    }

    [Test]
    public void Weekly_OutOfOrderInput_IsOrderedBeforeGrouping()
    {
        var daily = new[]
        {
            Day(2, 115m, 118m, 80m, 95m),
            Day(0, 100m, 120m, 90m, 110m),
            Day(1, 110m, 130m, 105m, 115m),
        };

        var weekly = CandleResampler.Resample(daily, CandleInterval.OneWeek);

        Assert.Multiple(() =>
        {
            Assert.That(weekly[0].Open, Is.EqualTo(100m));  // earliest day's open despite input order
            Assert.That(weekly[0].Close, Is.EqualTo(95m));  // latest day's close
        });
    }

    // ─── 4-hour aggregation ────────────────────────────────────────────────────

    [Test]
    public void OneHour_IsPassThrough()
    {
        var hourly = new[] { Hour(0, 100, 110, 95, 105), Hour(1, 105, 115, 100, 112) };

        Assert.That(CandleResampler.Resample(hourly, CandleInterval.OneHour), Is.SameAs(hourly));
    }

    [Test]
    public void FourHour_AggregatesHourlyIntoUtcAlignedBuckets()
    {
        // 00:00–07:00 → two buckets: [00–04) = hours 0-3, [04–08) = hours 4-7.
        var hourly = new[]
        {
            Hour(0, 100m, 105m, 99m, 101m, 1m),
            Hour(1, 101m, 108m, 100m, 104m, 1m),
            Hour(2, 104m, 106m, 95m, 100m, 1m),
            Hour(3, 100m, 102m, 98m, 99m, 1m),
            Hour(4, 99m, 112m, 98m, 110m, 1m),
            Hour(5, 110m, 115m, 108m, 113m, 1m),
            Hour(6, 113m, 114m, 90m, 92m, 1m),
            Hour(7, 92m, 96m, 88m, 95m, 1m),
        };

        var fourHour = CandleResampler.Resample(hourly, CandleInterval.FourHours);

        Assert.That(fourHour, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            // Bucket 1 (00:00): open of hour 0, max high, min low, close of hour 3, summed volume.
            Assert.That(fourHour[0].OpenTime, Is.EqualTo(Monday));
            Assert.That(fourHour[0].Open, Is.EqualTo(100m));
            Assert.That(fourHour[0].High, Is.EqualTo(108m));
            Assert.That(fourHour[0].Low, Is.EqualTo(95m));
            Assert.That(fourHour[0].Close, Is.EqualTo(99m));
            Assert.That(fourHour[0].Volume, Is.EqualTo(4m));
            // Bucket 2 (04:00): close of hour 7, min low across 4-7.
            Assert.That(fourHour[1].OpenTime, Is.EqualTo(Monday.AddHours(4)));
            Assert.That(fourHour[1].Open, Is.EqualTo(99m));
            Assert.That(fourHour[1].Low, Is.EqualTo(88m));
            Assert.That(fourHour[1].Close, Is.EqualTo(95m));
        });
    }

    [Test]
    public void FourHour_PartialBucket_ClosesWithAvailableBars()
    {
        // Hours 3 and 4 straddle the 04:00 boundary → hour 3 in bucket 1, hour 4 in bucket 2.
        var hourly = new[] { Hour(3, 100m, 101m, 99m, 100m), Hour(4, 100m, 105m, 100m, 104m) };

        var fourHour = CandleResampler.Resample(hourly, CandleInterval.FourHours);

        Assert.Multiple(() =>
        {
            Assert.That(fourHour, Has.Count.EqualTo(2));
            Assert.That(fourHour[0].OpenTime, Is.EqualTo(Monday));            // bucket [00-04)
            Assert.That(fourHour[1].OpenTime, Is.EqualTo(Monday.AddHours(4))); // bucket [04-08)
        });
    }
}
