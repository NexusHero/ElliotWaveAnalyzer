using System.Globalization;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The hallucination guard for vision-extracted counts: every claimed pivot must snap to a real candle
/// extreme (high or low) within tolerance of both its date and its price, or it is rejected with a
/// reason. Pivots come from perception; only after they lock onto actual data may a rule touch them.
/// Pure and static — a deterministic function of the claim and the candles.
/// </summary>
public static class PivotSnapper
{
    /// <summary>Default price tolerance (0.5%) for matching a claimed price to a candle extreme.</summary>
    public const decimal DefaultPricePercent = 0.5m;

    /// <summary>Default date tolerance in bars around the nearest candle.</summary>
    public const int DefaultBarTolerance = 1;

    /// <summary>
    /// Snaps each claimed pivot to the nearest in-tolerance candle extreme. Returns the snapped pivots
    /// and, separately, the rejected ones (each with a human-readable reason).
    /// </summary>
    public static (IReadOnlyList<SnappedPivot> Snapped, IReadOnlyList<RejectedPivot> Rejected) Snap(
        IReadOnlyList<ClaimedPivot> claimed,
        IReadOnlyList<MarketCandle> candles,
        decimal pricePercent = DefaultPricePercent,
        int barTolerance = DefaultBarTolerance)
    {
        ArgumentNullException.ThrowIfNull(claimed);
        ArgumentNullException.ThrowIfNull(candles);

        var snapped = new List<SnappedPivot>();
        var rejected = new List<RejectedPivot>();
        var tolerance = pricePercent / 100m;

        foreach (var pivot in claimed)
        {
            var match = BestExtreme(pivot, candles, tolerance, barTolerance);
            if (match is { } m)
            {
                snapped.Add(new SnappedPivot(pivot.Label, m.Date, m.Price, pivot.ApproxPrice));
            }
            else
            {
                rejected.Add(new RejectedPivot(
                    pivot.Label, pivot.ApproxDate, pivot.ApproxPrice,
                    $"claimed pivot at {Fmt(pivot.ApproxPrice)} on {pivot.ApproxDate:MMM d} — " +
                    $"no such extreme within ±{pricePercent}%"));
            }
        }

        return (snapped, rejected);
    }

    /// <summary>The closest candle extreme (high or low) to the pivot's price within both tolerances.</summary>
    private static (DateTime Date, decimal Price)? BestExtreme(
        ClaimedPivot pivot, IReadOnlyList<MarketCandle> candles, decimal tolerance, int barTolerance)
    {
        if (candles.Count == 0)
        {
            return null;
        }

        var nearest = NearestIndexByDate(pivot.ApproxDate, candles);
        var lo = Math.Max(0, nearest - barTolerance);
        var hi = Math.Min(candles.Count - 1, nearest + barTolerance);

        (DateTime Date, decimal Price)? best = null;
        var bestDiff = decimal.MaxValue;
        for (var i = lo; i <= hi; i++)
        {
            foreach (var extreme in new[] { candles[i].High, candles[i].Low })
            {
                if (extreme <= 0m)
                {
                    continue;
                }

                var diff = Math.Abs(pivot.ApproxPrice - extreme) / extreme;
                if (diff <= tolerance && diff < bestDiff)
                {
                    bestDiff = diff;
                    best = (candles[i].OpenTime, extreme);
                }
            }
        }

        return best;
    }

    private static int NearestIndexByDate(DateTime date, IReadOnlyList<MarketCandle> candles)
    {
        var nearest = 0;
        var bestDelta = TimeSpan.MaxValue;
        for (var i = 0; i < candles.Count; i++)
        {
            var delta = (candles[i].OpenTime - date).Duration();
            if (delta < bestDelta)
            {
                bestDelta = delta;
                nearest = i;
            }
        }

        return nearest;
    }

    private static string Fmt(decimal value)
        => value.ToString(value >= 1000m ? "N0" : "0.####", CultureInfo.InvariantCulture);
}
