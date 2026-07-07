using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="QuotaPeriodCalculator"/>: deterministic, epoch-anchored quota period boundaries
/// (#174, AC4).
/// </summary>
[TestFixture]
public sealed class QuotaPeriodCalculatorTests
{
    [Test]
    public void CurrentPeriodStart_OneDayPeriod_IsUtcMidnightOfTheGivenDay()
    {
        var now = new DateTimeOffset(2026, 3, 15, 14, 30, 0, TimeSpan.Zero);

        var start = QuotaPeriodCalculator.CurrentPeriodStart(now, periodDays: 1);

        Assert.That(start, Is.EqualTo(new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void CurrentPeriodStart_SameDayDifferentTimes_ReturnTheSamePeriod()
    {
        var morning = new DateTimeOffset(2026, 3, 15, 1, 0, 0, TimeSpan.Zero);
        var night = new DateTimeOffset(2026, 3, 15, 23, 59, 0, TimeSpan.Zero);

        Assert.That(
            QuotaPeriodCalculator.CurrentPeriodStart(morning, 1),
            Is.EqualTo(QuotaPeriodCalculator.CurrentPeriodStart(night, 1)));
    }

    [Test]
    public void CurrentPeriodStart_NextDay_IsADifferentPeriod()
    {
        var day1 = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero);
        var day2 = new DateTimeOffset(2026, 3, 16, 0, 0, 1, TimeSpan.Zero);

        Assert.That(
            QuotaPeriodCalculator.CurrentPeriodStart(day1, 1),
            Is.Not.EqualTo(QuotaPeriodCalculator.CurrentPeriodStart(day2, 1)));
    }

    [Test]
    public void CurrentPeriodStart_MultiDayPeriod_GroupsDaysTogether()
    {
        // A 7-day period anchored at the Unix epoch (a Thursday) — any two dates within the same
        // 7-day window from the epoch land on the same period start.
        var a = new DateTimeOffset(2026, 3, 12, 1, 0, 0, TimeSpan.Zero);
        var b = new DateTimeOffset(2026, 3, 15, 23, 0, 0, TimeSpan.Zero);

        Assert.That(
            QuotaPeriodCalculator.CurrentPeriodStart(a, 7),
            Is.EqualTo(QuotaPeriodCalculator.CurrentPeriodStart(b, 7)));
    }

    [Test]
    public void PeriodEnd_IsPeriodStartPlusPeriodDays()
    {
        var start = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        Assert.That(
            QuotaPeriodCalculator.PeriodEnd(start, 1),
            Is.EqualTo(new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void CurrentPeriodStart_ZeroOrNegativePeriodDays_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => QuotaPeriodCalculator.CurrentPeriodStart(DateTimeOffset.UtcNow, 0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => QuotaPeriodCalculator.CurrentPeriodStart(DateTimeOffset.UtcNow, -1));
        });
    }
}
