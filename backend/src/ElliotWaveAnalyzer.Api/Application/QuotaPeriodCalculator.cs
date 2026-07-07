namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure computation of the current quota period's boundaries (#174) — fixed-length windows anchored
/// at the Unix epoch, so the boundary is deterministic and reproducible from "now" alone (no stored
/// anchor to drift or need seeding). No I/O.
/// </summary>
public static class QuotaPeriodCalculator
{
    /// <summary>The start (UTC midnight) of the <paramref name="periodDays"/>-day window containing <paramref name="now"/>.</summary>
    public static DateTimeOffset CurrentPeriodStart(DateTimeOffset now, int periodDays)
    {
        if (periodDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(periodDays), periodDays, "Period must be at least 1 day.");
        }

        var daysSinceEpoch = now.UtcDateTime.Date.Subtract(DateTime.UnixEpoch).Days;
        var periodIndex = daysSinceEpoch / periodDays;
        return new DateTimeOffset(DateTime.UnixEpoch.AddDays(periodIndex * periodDays), TimeSpan.Zero);
    }

    /// <summary>The exclusive end of the period <paramref name="periodStart"/> begins — when the quota resets.</summary>
    public static DateTimeOffset PeriodEnd(DateTimeOffset periodStart, int periodDays) => periodStart.AddDays(periodDays);
}
