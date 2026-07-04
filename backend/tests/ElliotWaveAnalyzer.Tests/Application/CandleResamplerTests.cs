using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="CandleResampler"/>: daily pass-through and weekly
/// OHLCV aggregation by ISO week.
/// </summary>
[TestFixture]
public sealed class CandleResamplerTests
{
    // 2024-01-01 is a Monday, so a clean ISO-week boundary.
    private static readonly DateTime Monday = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static MarketCandle Day(int offset, decimal open, decimal high, decimal low, decimal close, decimal vol = 0m)
        => new(Monday.AddDays(offset), open, high, low, close, vol);

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
}
