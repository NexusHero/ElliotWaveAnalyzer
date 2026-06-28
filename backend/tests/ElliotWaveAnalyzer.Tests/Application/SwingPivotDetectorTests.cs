using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the deterministic <see cref="SwingPivotDetector"/> (ZigZag).
/// </summary>
[TestFixture]
public sealed class SwingPivotDetectorTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds candles from close prices (OHLC all = close, which is all the detector reads).</summary>
    private static IReadOnlyList<MarketCandle> Candles(params decimal[] closes)
        => [.. closes.Select((c, i) => new MarketCandle(Start.AddDays(i), c, c, c, c, 0m))];

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
}
