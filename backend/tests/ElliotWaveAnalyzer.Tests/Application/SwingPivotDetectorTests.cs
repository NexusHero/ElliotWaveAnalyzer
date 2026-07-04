using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the deterministic <see cref="SwingPivotDetector"/> (ZigZag).
/// </summary>
[TestFixture]
public sealed class SwingPivotDetectorTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds flat candles from close prices (OHLC all = close).</summary>
    private static IReadOnlyList<MarketCandle> Candles(params decimal[] closes)
        => [.. closes.Select((c, i) => new MarketCandle(Start.AddDays(i), c, c, c, c, 0m))];

    /// <summary>ATR per candle via the real Skender calculator — the same series production
    /// would feed into <see cref="SwingPivotDetector.DetectAtrAdaptive"/>. Exercises the seam
    /// end-to-end (calculator → detector) with no hand-rolled ATR.</summary>
    private static IReadOnlyList<decimal?> Atr(IReadOnlyList<MarketCandle> candles, int period = 14)
        => [.. new SkenderIndicatorCalculator().CalculateAtr(candles, period).Select(r => r.Value)];

    /// <summary>Builds candles with distinct highs/lows: (high, low) pairs, open/close mid-range.</summary>
    private static IReadOnlyList<MarketCandle> HlCandles(params (decimal High, decimal Low)[] bars)
        => [.. bars.Select((b, i) =>
        {
            var mid = (b.High + b.Low) / 2m;
            return new MarketCandle(Start.AddDays(i), mid, b.High, b.Low, mid, 0m);
        })];

    [Test]
    public void TooFewCandles_ReturnsEmpty()
    {
        Assert.That(SwingPivotDetector.Detect(Candles(100m)), Is.Empty);
    }

    [Test]
    public void NoMoveReachesThreshold_ReturnsEmpty()
    {
        // 2% drift never crosses a 5% threshold.
        var pivots = SwingPivotDetector.Detect(Candles(100m, 101m, 102m, 101m, 102m), thresholdPercent: 5m);

        Assert.That(pivots, Is.Empty);
    }

    [Test]
    public void SingleUpThenDownLeg_DetectsLowHighThenTrailingLow()
    {
        // Up to 130 (a +30% high), then down to 110 (>10% reversal confirms the high).
        // The final leg's running extreme (the trailing low) is also emitted, so the most
        // recent — still in-progress — swing point is included for counting.
        var pivots = SwingPivotDetector.Detect(
            Candles(100m, 110m, 130m, 120m, 110m), thresholdPercent: 10m);

        Assert.That(pivots, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(pivots[0].IsHigh, Is.False);          // origin low
            Assert.That(pivots[0].Price, Is.EqualTo(100m));
            Assert.That(pivots[1].IsHigh, Is.True);           // confirmed high
            Assert.That(pivots[1].Price, Is.EqualTo(130m));
            Assert.That(pivots[2].IsHigh, Is.False);          // trailing low (in-progress leg)
            Assert.That(pivots[2].Price, Is.EqualTo(110m));
        });
    }

    [Test]
    public void Pivots_StrictlyAlternateHighLow()
    {
        // Zig-zag: 100 ↑150 ↓120 ↑180 ↓140 ↑200
        var pivots = SwingPivotDetector.Detect(
            Candles(100m, 150m, 120m, 180m, 140m, 200m), thresholdPercent: 10m);

        Assert.That(pivots, Is.Not.Empty);
        for (var i = 1; i < pivots.Count; i++)
        {
            Assert.That(pivots[i].IsHigh, Is.Not.EqualTo(pivots[i - 1].IsHigh),
                $"pivot {i} does not alternate");
        }
    }

    [Test]
    public void ZeroThreshold_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SwingPivotDetector.Detect(Candles(100m, 110m), thresholdPercent: 0m));
    }

    [Test]
    public void WickExtreme_IsPreferredOverClose()
    {
        // Bar 2 closes mid-range but its wick spikes to 140 — the pivot must be the wick.
        var candles = HlCandles((101m, 99m), (116m, 104m), (140m, 118m), (125m, 111m), (112m, 100m));

        var pivots = SwingPivotDetector.Detect(candles, thresholdPercent: 10m);

        var high = pivots.Single(p => p.IsHigh);
        Assert.Multiple(() =>
        {
            Assert.That(high.Price, Is.EqualTo(140m));
            Assert.That(high.Date, Is.EqualTo(Start.AddDays(2)));
        });
    }

    [Test]
    public void OpeningPivot_IsTheRunningExtreme_NotTheFirstCandle()
    {
        // Price dips to 95 on bar 1 before rallying: the origin low must be 95, not bar 0.
        var candles = HlCandles((102m, 98m), (99m, 95m), (108m, 101m), (120m, 110m), (108m, 100m));

        var pivots = SwingPivotDetector.Detect(candles, thresholdPercent: 10m);

        Assert.That(pivots[0].IsHigh, Is.False);
        Assert.That(pivots[0].Price, Is.EqualTo(95m));
        Assert.That(pivots[0].Date, Is.EqualTo(Start.AddDays(1)));
    }

    [Test]
    public void ExtendAndReverseOnSameCandle_ExtendWins()
    {
        // Bar 2 makes a new high AND has a low deep enough to reverse — the intrabar order is
        // unknowable, so the leg extends and the reversal is only taken on a later candle.
        var candles = HlCandles((101m, 99m), (120m, 105m), (130m, 90m), (95m, 85m));

        var pivots = SwingPivotDetector.Detect(candles, thresholdPercent: 10m);

        var high = pivots.Single(p => p.IsHigh);
        Assert.That(high.Price, Is.EqualTo(130m), "the new high on the wide candle must win");
    }

    // ─── ATR-adaptive mode ─────────────────────────────────────────────────────

    [Test]
    public void AtrAdaptive_InvalidArguments_Throw()
    {
        var candles = Candles(100m, 110m);
        var atr = Atr(candles);
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SwingPivotDetector.DetectAtrAdaptive(candles, atr, atrMultiplier: 0m));
            // ATR series length must match the candle count.
            Assert.Throws<ArgumentException>(
                () => SwingPivotDetector.DetectAtrAdaptive(candles, [null], atrMultiplier: 3m));
        });
    }

    [Test]
    public void AtrAdaptive_QuietRegime_FindsSwingsAFixedThresholdMisses()
    {
        // ~0.5%-range bars with 2% swings: invisible to a fixed 5% ZigZag, but 2% is several
        // ATRs in this regime, so the adaptive detector confirms the swings.
        var closes = new List<decimal>();
        for (var cycle = 0; cycle < 4; cycle++)
        {
            for (var i = 0; i < 8; i++)
            {
                closes.Add(100m + i * 0.25m);  // drift up to 101.75
            }
            for (var i = 0; i < 8; i++)
            {
                closes.Add(101.75m - i * 0.25m); // drift back down
            }
        }
        var candles = Candles([.. closes]);

        var fixedPivots = SwingPivotDetector.Detect(candles, thresholdPercent: 5m);
        var adaptivePivots = SwingPivotDetector.DetectAtrAdaptive(candles, Atr(candles), atrMultiplier: 3m);

        Assert.Multiple(() =>
        {
            Assert.That(fixedPivots, Is.Empty, "5% fixed threshold must not see 2% swings");
            Assert.That(adaptivePivots, Has.Count.GreaterThanOrEqualTo(4),
                "3×ATR must confirm the 2% swings in a quiet regime");
        });
    }

    [Test]
    public void AtrAdaptive_Pivots_StrictlyAlternate()
    {
        var candles = Candles(100m, 106m, 99m, 108m, 101m, 112m, 103m, 115m, 104m, 118m,
            106m, 120m, 108m, 124m, 110m, 128m, 112m, 132m, 114m, 136m);

        var pivots = SwingPivotDetector.DetectAtrAdaptive(candles, Atr(candles, period: 5), atrMultiplier: 1m);

        Assert.That(pivots, Is.Not.Empty);
        for (var i = 1; i < pivots.Count; i++)
        {
            Assert.That(pivots[i].IsHigh, Is.Not.EqualTo(pivots[i - 1].IsHigh),
                $"pivot {i} does not alternate");
        }
    }

    // ─── Multi-scale mode ──────────────────────────────────────────────────────

    /// <summary>A series with small 3% wiggles riding 15% swings riding a 40% trend.</summary>
    private static IReadOnlyList<MarketCandle> NestedSwings()
    {
        var closes = new List<decimal>();
        var price = 100m;
        // Three large up-legs with medium pullbacks, each leg carrying small wiggles.
        for (var leg = 0; leg < 3; leg++)
        {
            for (var i = 0; i < 10; i++)
            {
                price *= i % 3 == 2 ? 0.985m : 1.03m; // wiggly climb
                closes.Add(price);
            }
            for (var i = 0; i < 4; i++)
            {
                price *= 0.965m; // medium pullback
                closes.Add(price);
            }
        }
        return Candles([.. closes]);
    }

    [Test]
    public void MultiScale_DefaultRun_TagsThreeDegreesFinestFirst()
    {
        var scales = SwingPivotDetector.DetectMultiScale(NestedSwings());

        Assert.That(scales, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(scales[0].Degree, Is.EqualTo(WaveDegree.Minor));
            Assert.That(scales[1].Degree, Is.EqualTo(WaveDegree.Intermediate));
            Assert.That(scales[2].Degree, Is.EqualTo(WaveDegree.Primary));
            Assert.That(scales[0].Pivots, Has.Count.GreaterThanOrEqualTo(scales[1].Pivots.Count));
            Assert.That(scales[1].Pivots, Has.Count.GreaterThanOrEqualTo(scales[2].Pivots.Count));
        });
    }

    [Test]
    public void MultiScale_CoarsePivots_AreSubsetOfFinerScale()
    {
        var scales = SwingPivotDetector.DetectMultiScale(NestedSwings(), [2m, 6m, 14m]);

        for (var s = 1; s < scales.Count; s++)
        {
            var finer = scales[s - 1].Pivots.ToHashSet();
            foreach (var pivot in scales[s].Pivots)
            {
                Assert.That(finer, Does.Contain(pivot),
                    $"coarse pivot {pivot.Date:yyyy-MM-dd}@{pivot.Price} missing from scale {s - 1}");
            }
        }
    }

    [Test]
    public void MultiScale_EveryScale_StrictlyAlternates()
    {
        var scales = SwingPivotDetector.DetectMultiScale(NestedSwings(), [2m, 6m, 14m]);

        foreach (var scale in scales)
        {
            for (var i = 1; i < scale.Pivots.Count; i++)
            {
                Assert.That(scale.Pivots[i].IsHigh, Is.Not.EqualTo(scale.Pivots[i - 1].IsHigh),
                    $"{scale.Degree} pivot {i} does not alternate");
            }
        }
    }

    [Test]
    public void MultiScale_NonAscendingThresholds_Throw()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(
                () => SwingPivotDetector.DetectMultiScale(NestedSwings(), [3m, 3m]));
            Assert.Throws<ArgumentException>(
                () => SwingPivotDetector.DetectMultiScale(NestedSwings(), [6m, 3m]));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SwingPivotDetector.DetectMultiScale(NestedSwings(), []));
        });
    }

    [Test]
    public void MultiScale_SingleScale_MatchesPlainDetect()
    {
        var candles = NestedSwings();

        var scales = SwingPivotDetector.DetectMultiScale(candles, [3m]);

        Assert.Multiple(() =>
        {
            Assert.That(scales, Has.Count.EqualTo(1));
            Assert.That(scales[0].Degree, Is.EqualTo(WaveDegree.Primary));
            Assert.That(scales[0].Pivots, Is.EqualTo(SwingPivotDetector.Detect(candles, 3m)));
        });
    }
}
