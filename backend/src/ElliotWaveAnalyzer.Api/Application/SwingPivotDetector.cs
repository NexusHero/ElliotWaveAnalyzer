using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic ZigZag swing detector. Walks the candle series and records a pivot every
/// time price reverses by at least a threshold percentage from the running extreme, yielding
/// a sequence of alternating swing highs and lows.
///
/// WHY deterministic (not LLM): picking exact turning points from a numeric series is
/// geometry, not language — LLMs are unreliable at it. This stage gives the LLM clean,
/// rule-checkable candidate pivots so it can do what it is good at (judgement + explanation)
/// instead of inventing prices.
///
/// Pure (static, no I/O) so it can be unit-tested exhaustively without mocks. Close prices
/// are used for the reversal test, which keeps the algorithm simple and robust; the pivot
/// price is therefore the candle close at the extreme.
/// </summary>
public static class SwingPivotDetector
{
    /// <summary>
    /// Detects swing pivots in <paramref name="candles"/>.
    /// </summary>
    /// <param name="candles">OHLCV candles, ascending by time.</param>
    /// <param name="thresholdPercent">
    /// Minimum percentage reversal from the running extreme that confirms a swing
    /// (e.g. 3 = 3%). Larger values yield fewer, larger swings.
    /// </param>
    /// <returns>Alternating high/low pivots in chronological order (empty if too few candles
    /// or no move ever reaches the threshold).</returns>
    public static IReadOnlyList<SwingPivot> Detect(
        IReadOnlyList<MarketCandle> candles, decimal thresholdPercent = 3m)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (thresholdPercent <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thresholdPercent), thresholdPercent, "Threshold must be positive.");
        }

        var pivots = new List<SwingPivot>();
        if (candles.Count < 2)
        {
            return pivots;
        }

        var threshold = thresholdPercent / 100m;

        // extremeIdx tracks the most extreme candle in the current leg.
        // trend: 0 = direction not yet established, +1 = up-leg (extreme is a high),
        //        -1 = down-leg (extreme is a low). It flips each time a pivot is confirmed,
        //        which is what makes the output strictly alternate high/low.
        var extremeIdx = 0;
        var trend = 0;

        for (var i = 1; i < candles.Count; i++)
        {
            var price = candles[i].Close;
            var extremePrice = candles[extremeIdx].Close;
            if (extremePrice == 0)
            {
                continue;
            }

            switch (trend)
            {
                case 1:
                    if (price > extremePrice)
                    {
                        extremeIdx = i; // extend the up-leg to a new high
                    }
                    else if ((extremePrice - price) / extremePrice >= threshold)
                    {
                        pivots.Add(ToPivot(candles[extremeIdx], isHigh: true));
                        extremeIdx = i;
                        trend = -1;
                    }
                    break;

                case -1:
                    if (price < extremePrice)
                    {
                        extremeIdx = i; // extend the down-leg to a new low
                    }
                    else if ((price - extremePrice) / extremePrice >= threshold)
                    {
                        pivots.Add(ToPivot(candles[extremeIdx], isHigh: false));
                        extremeIdx = i;
                        trend = 1;
                    }
                    break;

                default: // trend == 0: establish direction from the first threshold move
                    if ((price - extremePrice) / extremePrice >= threshold)
                    {
                        pivots.Add(ToPivot(candles[extremeIdx], isHigh: false)); // start was a low
                        extremeIdx = i;
                        trend = 1;
                    }
                    else if ((extremePrice - price) / extremePrice >= threshold)
                    {
                        pivots.Add(ToPivot(candles[extremeIdx], isHigh: true)); // start was a high
                        extremeIdx = i;
                        trend = -1;
                    }
                    break;
            }
        }

        // Close out the final leg: the running extreme is the most recent pivot.
        if (trend == 1)
        {
            pivots.Add(ToPivot(candles[extremeIdx], isHigh: true));
        }
        else if (trend == -1)
        {
            pivots.Add(ToPivot(candles[extremeIdx], isHigh: false));
        }

        return pivots;

        static SwingPivot ToPivot(MarketCandle c, bool isHigh) => new(c.OpenTime, c.Close, isHigh);
    }
}
