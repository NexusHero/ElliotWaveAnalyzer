using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Decides whether a "price entered the entry zone" alert should fire. Price has entered the zone
/// when any candle's range overlaps [low, high] (wick-aware). Fires at most once per analysis: once
/// <paramref name="alreadyAlerted"/> is set the decision is always false, so re-runs don't spam.
/// Pure and deterministic — the idempotency flag is persisted by the caller.
/// </summary>
public static class ZoneEntryDecision
{
    /// <summary>
    /// True when the entry zone is defined, has not been alerted yet, and at least one of
    /// <paramref name="candlesAfter"/> traded inside it.
    /// </summary>
    public static bool ShouldAlert(
        decimal? low, decimal? high, bool alreadyAlerted, IReadOnlyList<MarketCandle> candlesAfter)
    {
        ArgumentNullException.ThrowIfNull(candlesAfter);

        if (alreadyAlerted || low is not { } lo || high is not { } hi)
        {
            return false;
        }

        if (lo > hi)
        {
            (lo, hi) = (hi, lo);
        }

        // A candle entered the zone if its [Low, High] range overlaps the zone band.
        return candlesAfter.Any(c => c.High >= lo && c.Low <= hi);
    }
}
