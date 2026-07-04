using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Deterministic ZigZag swing detector. Walks the candle series and records a pivot every
/// time price reverses by at least a threshold from the running extreme, yielding a sequence
/// of strictly alternating swing highs and lows.
///
/// WHY deterministic (not LLM): picking exact turning points from a numeric series is
/// geometry, not language — LLMs are unreliable at it. This stage gives the LLM clean,
/// rule-checkable candidate pivots so it can do what it is good at (judgement + explanation)
/// instead of inventing prices.
///
/// The detector is wick-aware: in an up-leg the running extreme is the candle High, in a
/// down-leg the candle Low, and reversals are confirmed against the opposite wick — so pivots
/// land on the true swing extremes rather than on closes. When a single candle both extends
/// the extreme and would qualify as a reversal, extending wins (the intrabar order of High
/// and Low is unknowable from OHLC, so the ambiguous reversal is deliberately not taken).
///
/// Three modes:
///  - <see cref="Detect"/> — fixed percent threshold (the classic ZigZag).
///  - <see cref="DetectAtrAdaptive"/> — threshold scales with volatility (k × ATR).
///  - <see cref="DetectMultiScale"/> — several thresholds at once, tagged with Elliott
///    degrees, coarse scales guaranteed to be subsets of finer ones.
///
/// Pure (static, no I/O) so it can be unit-tested exhaustively without mocks.
/// </summary>
public static class SwingPivotDetector
{
    /// <summary>
    /// Detects swing pivots with a fixed percentage reversal threshold.
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

        var fraction = thresholdPercent / 100m;
        return DetectCore(candles, (_, extremePrice) => extremePrice * fraction);
    }

    /// <summary>
    /// Detects swing pivots with a volatility-adaptive threshold: a reversal confirms when
    /// price moves at least <paramref name="atrMultiplier"/> × ATR(<paramref name="atrPeriod"/>)
    /// against the running extreme. In quiet regimes this finds swings a fixed percent would
    /// miss; in volatile regimes it ignores noise a fixed percent would count.
    /// </summary>
    /// <param name="candles">OHLCV candles, ascending by time.</param>
    /// <param name="atrMultiplier">How many ATRs of adverse movement confirm a reversal.</param>
    /// <param name="atrPeriod">ATR lookback (Wilder smoothing). Candles inside the warm-up
    /// window only track extremes; reversals are confirmed once ATR is available.</param>
    public static IReadOnlyList<SwingPivot> DetectAtrAdaptive(
        IReadOnlyList<MarketCandle> candles, decimal atrMultiplier = 3m, int atrPeriod = 14)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (atrMultiplier <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atrMultiplier), atrMultiplier, "ATR multiplier must be positive.");
        }
        if (atrPeriod < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(atrPeriod), atrPeriod, "ATR period must be at least 1.");
        }

        var atr = WilderAtr(candles, atrPeriod);
        return DetectCore(candles, (i, _) => atr[i] is { } a ? atrMultiplier * a : null);
    }

    /// <summary>Default thresholds for <see cref="DetectMultiScale"/>, finest → coarsest.</summary>
    public static readonly IReadOnlyList<decimal> DefaultScaleThresholds = [1.5m, 3m, 6m];

    /// <summary>
    /// Detects pivots at several reversal thresholds at once and tags each scale with an
    /// Elliott degree (three scales map to Minor / Intermediate / Primary). The finest scale
    /// is detected from candles; every coarser scale is derived by compressing the next finer
    /// pivot sequence, so coarse pivots are a strict subset of finer ones by construction —
    /// the invariant the nested wave parser relies on to relate degrees.
    /// </summary>
    /// <param name="candles">OHLCV candles, ascending by time.</param>
    /// <param name="thresholdsPercent">1–5 thresholds in strictly ascending order
    /// (finest → coarsest); defaults to <see cref="DefaultScaleThresholds"/>.</param>
    /// <returns>One <see cref="PivotScale"/> per threshold, finest first.</returns>
    public static IReadOnlyList<PivotScale> DetectMultiScale(
        IReadOnlyList<MarketCandle> candles, IReadOnlyList<decimal>? thresholdsPercent = null)
    {
        ArgumentNullException.ThrowIfNull(candles);
        var thresholds = thresholdsPercent ?? DefaultScaleThresholds;

        if (thresholds.Count is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(
                nameof(thresholdsPercent), thresholds.Count, "Provide between 1 and 5 thresholds.");
        }
        for (var i = 0; i < thresholds.Count; i++)
        {
            if (thresholds[i] <= 0 || (i > 0 && thresholds[i] <= thresholds[i - 1]))
            {
                throw new ArgumentException(
                    "Thresholds must be positive and strictly ascending (finest → coarsest).",
                    nameof(thresholdsPercent));
            }
        }

        // Degrees are assigned so the coarsest scale lands on Primary when four or fewer
        // scales are requested (e.g. three scales → Minor, Intermediate, Primary).
        ReadOnlySpan<WaveDegree> ladder =
            [WaveDegree.Minute, WaveDegree.Minor, WaveDegree.Intermediate, WaveDegree.Primary, WaveDegree.Cycle];
        var offset = Math.Max(0, (int)WaveDegree.Primary - (thresholds.Count - 1));

        var scales = new List<PivotScale>(thresholds.Count);
        var pivots = Detect(candles, thresholds[0]);
        scales.Add(new PivotScale(ladder[offset], thresholds[0], pivots));

        for (var s = 1; s < thresholds.Count; s++)
        {
            pivots = CompressPivots(pivots, thresholds[s] / 100m);
            scales.Add(new PivotScale(ladder[offset + s], thresholds[s], pivots));
        }

        return scales;
    }

    /// <summary>
    /// Core ZigZag walk. <paramref name="reversalDistance"/> maps (candle index, running
    /// extreme price) to the absolute adverse move that confirms a reversal there — or null
    /// while the threshold is not yet defined (ATR warm-up), during which only extremes are
    /// tracked. This is the single place the swing geometry lives; the public modes only
    /// differ in how the threshold is computed.
    /// </summary>
    private static List<SwingPivot> DetectCore(
        IReadOnlyList<MarketCandle> candles, Func<int, decimal, decimal?> reversalDistance)
    {
        var pivots = new List<SwingPivot>();
        if (candles.Count < 2)
        {
            return pivots;
        }

        // trend: 0 = direction not yet established, +1 = up-leg (extreme is a High),
        //        -1 = down-leg (extreme is a Low). It flips each time a pivot is confirmed,
        //        which is what makes the output strictly alternate high/low.
        // Before the first pivot, both the highest High and lowest Low are tracked so the
        // opening pivot is the true extreme, not merely the first candle.
        var trend = 0;
        var extremeIdx = 0;
        var maxIdx = 0;
        var minIdx = 0;

        for (var i = 1; i < candles.Count; i++)
        {
            var candle = candles[i];

            switch (trend)
            {
                case 1:
                    var extremeHigh = candles[extremeIdx].High;
                    if (candle.High > extremeHigh)
                    {
                        extremeIdx = i; // extend the up-leg to a new high
                    }
                    else if (extremeHigh > 0
                        && reversalDistance(i, extremeHigh) is { } down
                        && extremeHigh - candle.Low >= down)
                    {
                        pivots.Add(new SwingPivot(candles[extremeIdx].OpenTime, extremeHigh, IsHigh: true));
                        extremeIdx = i;
                        trend = -1;
                    }
                    break;

                case -1:
                    var extremeLow = candles[extremeIdx].Low;
                    if (candle.Low < extremeLow)
                    {
                        extremeIdx = i; // extend the down-leg to a new low
                    }
                    else if (extremeLow > 0
                        && reversalDistance(i, extremeLow) is { } up
                        && candle.High - extremeLow >= up)
                    {
                        pivots.Add(new SwingPivot(candles[extremeIdx].OpenTime, extremeLow, IsHigh: false));
                        extremeIdx = i;
                        trend = 1;
                    }
                    break;

                default: // trend == 0: establish direction from the first threshold move
                    if (candle.High > candles[maxIdx].High)
                    {
                        maxIdx = i;
                    }
                    if (candle.Low < candles[minIdx].Low)
                    {
                        minIdx = i;
                    }

                    var minLow = candles[minIdx].Low;
                    var maxHigh = candles[maxIdx].High;
                    // A giant opening candle could satisfy both directions at once; checking
                    // up first is an arbitrary but deterministic tie-break.
                    if (minLow > 0
                        && reversalDistance(i, minLow) is { } confirmUp
                        && candle.High - minLow >= confirmUp)
                    {
                        pivots.Add(new SwingPivot(candles[minIdx].OpenTime, minLow, IsHigh: false));
                        extremeIdx = i;
                        trend = 1;
                    }
                    else if (maxHigh > 0
                        && reversalDistance(i, maxHigh) is { } confirmDown
                        && maxHigh - candle.Low >= confirmDown)
                    {
                        pivots.Add(new SwingPivot(candles[maxIdx].OpenTime, maxHigh, IsHigh: true));
                        extremeIdx = i;
                        trend = -1;
                    }
                    break;
            }
        }

        // Close out the final leg: the running extreme is the most recent pivot.
        if (trend == 1)
        {
            pivots.Add(new SwingPivot(candles[extremeIdx].OpenTime, candles[extremeIdx].High, IsHigh: true));
        }
        else if (trend == -1)
        {
            pivots.Add(new SwingPivot(candles[extremeIdx].OpenTime, candles[extremeIdx].Low, IsHigh: false));
        }

        return pivots;
    }

    /// <summary>
    /// Re-runs the ZigZag state machine over an already-alternating pivot sequence with a
    /// larger relative threshold. Because it only ever emits members of the input, the result
    /// is a strict subset — the multi-scale invariant. Measuring pivot-to-pivot (instead of
    /// re-scanning candles) is exactly right here: the finer scale has already captured every
    /// extreme a coarser scan could find.
    /// </summary>
    private static IReadOnlyList<SwingPivot> CompressPivots(
        IReadOnlyList<SwingPivot> pivots, decimal thresholdFraction)
    {
        var result = new List<SwingPivot>();
        if (pivots.Count == 0)
        {
            return result;
        }

        var trend = 0;
        var extreme = pivots[0];
        SwingPivot? runningMax = null;
        SwingPivot? runningMin = null;

        foreach (var pivot in pivots)
        {
            switch (trend)
            {
                case 1:
                    if (pivot.IsHigh && pivot.Price > extreme.Price)
                    {
                        extreme = pivot;
                    }
                    else if (!pivot.IsHigh && extreme.Price > 0
                        && extreme.Price - pivot.Price >= extreme.Price * thresholdFraction)
                    {
                        result.Add(extreme);
                        extreme = pivot;
                        trend = -1;
                    }
                    break;

                case -1:
                    if (!pivot.IsHigh && pivot.Price < extreme.Price)
                    {
                        extreme = pivot;
                    }
                    else if (pivot.IsHigh && extreme.Price > 0
                        && pivot.Price - extreme.Price >= extreme.Price * thresholdFraction)
                    {
                        result.Add(extreme);
                        extreme = pivot;
                        trend = 1;
                    }
                    break;

                default:
                    if (pivot.IsHigh && (runningMax is null || pivot.Price > runningMax.Price))
                    {
                        runningMax = pivot;
                    }
                    if (!pivot.IsHigh && (runningMin is null || pivot.Price < runningMin.Price))
                    {
                        runningMin = pivot;
                    }

                    if (pivot.IsHigh && runningMin is { Price: > 0 }
                        && pivot.Price - runningMin.Price >= runningMin.Price * thresholdFraction)
                    {
                        result.Add(runningMin);
                        extreme = pivot;
                        trend = 1;
                    }
                    else if (!pivot.IsHigh && runningMax is { Price: > 0 }
                        && runningMax.Price - pivot.Price >= runningMax.Price * thresholdFraction)
                    {
                        result.Add(runningMax);
                        extreme = pivot;
                        trend = -1;
                    }
                    break;
            }
        }

        if (trend != 0)
        {
            result.Add(extreme);
        }

        return result;
    }

    /// <summary>
    /// Wilder-smoothed Average True Range per candle (null during the warm-up window).
    /// Implemented here rather than via Skender because the Application layer stays free of
    /// third-party contracts (Skender is isolated in Infrastructure by convention) — and ATR,
    /// unlike RSI/MACD, has no seeding subtleties beyond the documented Wilder recurrence.
    /// </summary>
    private static decimal?[] WilderAtr(IReadOnlyList<MarketCandle> candles, int period)
    {
        var atr = new decimal?[candles.Count];
        if (candles.Count <= period)
        {
            return atr;
        }

        var trueRanges = new decimal[candles.Count];
        for (var i = 1; i < candles.Count; i++)
        {
            var c = candles[i];
            var prevClose = candles[i - 1].Close;
            trueRanges[i] = Math.Max(
                c.High - c.Low,
                Math.Max(Math.Abs(c.High - prevClose), Math.Abs(c.Low - prevClose)));
        }

        // First ATR = simple average of the first `period` true ranges (indices 1..period);
        // thereafter Wilder's recurrence.
        decimal sum = 0;
        for (var i = 1; i <= period; i++)
        {
            sum += trueRanges[i];
        }

        atr[period] = sum / period;
        for (var i = period + 1; i < candles.Count; i++)
        {
            atr[i] = (atr[i - 1]!.Value * (period - 1) + trueRanges[i]) / period;
        }

        return atr;
    }
}
