using ElliotWaveAnalyzer.Api.Infrastructure;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Infrastructure;

/// <summary>
/// Unit tests for <see cref="SkenderIndicatorCalculator"/>.
///
/// Strategy: test mathematical properties of RSI/MACD rather than
/// hard-coded output values. This makes tests robust against minor
/// Skender version changes while still catching real regressions.
/// </summary>
[TestFixture]
public sealed class SkenderIndicatorCalculatorTests
{
    private SkenderIndicatorCalculator _sut = null!;

    [SetUp]
    public void SetUp() => _sut = new SkenderIndicatorCalculator();

    // ─── RSI ──────────────────────────────────────────────────────────────────

    [Test]
    public void CalculateRsi_ResultCount_EqualsInputCount()
    {
        // Skender returns one result per input candle, padded with null for warm-up.
        var candles = MarketDataFixtures.CreateCandles(30);

        var result = _sut.CalculateRsi(candles);

        Assert.That(result.Count, Is.EqualTo(candles.Count));
    }

    [Test]
    public void CalculateRsi_ResultDates_AlignWithInputDates()
    {
        var candles = MarketDataFixtures.CreateCandles(30);

        var result = _sut.CalculateRsi(candles);

        Assert.That(
            result.Select(r => r.Date),
            Is.EqualTo(candles.Select(c => c.OpenTime)),
            "RSI dates must map 1:1 to input candle OpenTime");
    }

    [Test]
    public void CalculateRsi_WarmUpPeriod_HasNullValues()
    {
        // First `period` results are null because the EMA hasn't accumulated
        // enough data yet. Skender pads these with null rather than guessing.
        var candles = MarketDataFixtures.CreateCandles(20);
        const int period = 14;

        var result = _sut.CalculateRsi(candles, period);

        var warmUpValues = result.Take(period).Select(r => r.Value);
        Assert.That(warmUpValues, Has.All.Null,
            $"First {period} RSI values must be null (warm-up period)");
    }

    [Test]
    public void CalculateRsi_WithSufficientData_ValuesAreInZeroToHundredRange()
    {
        var candles = MarketDataFixtures.CreateCandles(50);

        var result = _sut.CalculateRsi(candles);

        var computed = result.Where(r => r.Value.HasValue).ToList();
        Assert.That(computed, Is.Not.Empty, "Should produce at least one non-null RSI value");
        Assert.That(computed.All(r => r.Value >= 0m && r.Value <= 100m), Is.True,
            "RSI must always be in [0, 100]");
    }

    [Test]
    [Description("Mathematical property: all gains → no losses → RS=∞ → RSI→100")]
    public void CalculateRsi_AllGains_RsiApproachesOneHundred()
    {
        // After the warm-up period, with zero losses the RSI must be at or very near 100.
        var candles = MarketDataFixtures.CreateAllGainsCandles(count: 30);

        var result = _sut.CalculateRsi(candles, period: 14);

        var lastRsi = result.Last(r => r.Value.HasValue).Value!.Value;
        Assert.That((double)lastRsi, Is.GreaterThan(99.0),
            "RSI with all gains must be > 99");
    }

    [Test]
    [Description("Mathematical property: all losses → no gains → RS=0 → RSI→0")]
    public void CalculateRsi_AllLosses_RsiApproachesZero()
    {
        var candles = MarketDataFixtures.CreateAllLossesCandles(count: 30);

        var result = _sut.CalculateRsi(candles, period: 14);

        var lastRsi = result.Last(r => r.Value.HasValue).Value!.Value;
        Assert.That((double)lastRsi, Is.LessThan(1.0),
            "RSI with all losses must be < 1");
    }

    // ─── MACD ─────────────────────────────────────────────────────────────────

    [Test]
    public void CalculateMacd_ResultCount_EqualsInputCount()
    {
        var candles = MarketDataFixtures.CreateCandles(50);

        var result = _sut.CalculateMacd(candles);

        Assert.That(result.Count, Is.EqualTo(candles.Count));
    }

    [Test]
    public void CalculateMacd_ResultDates_AlignWithInputDates()
    {
        var candles = MarketDataFixtures.CreateCandles(50);

        var result = _sut.CalculateMacd(candles);

        Assert.That(
            result.Select(r => r.Date),
            Is.EqualTo(candles.Select(c => c.OpenTime)));
    }

    [Test]
    public void CalculateMacd_WarmUpPeriod_HasNullComponents()
    {
        // MACD needs slowPeriods (26) of data. Before that, all components are null.
        var candles = MarketDataFixtures.CreateCandles(50);
        const int slowPeriods = 26;

        var result = _sut.CalculateMacd(candles, slowPeriods: slowPeriods);

        // The first (slowPeriods - 1) entries must be null
        var warmUp = result.Take(slowPeriods - 1).ToList();
        Assert.That(warmUp.All(r => r.MacdLine is null && r.SignalLine is null), Is.True,
            "MACD warm-up entries must have null components");
    }

    [Test]
    [Description("FastEMA > SlowEMA in uptrend → MacdLine > 0")]
    public void CalculateMacd_StrongUptrend_MacdLineIsPositive()
    {
        // A consistent uptrend causes the fast EMA (12) to track above the slow EMA (26).
        var candles = MarketDataFixtures.CreateTrendingCandles(uptrend: true, count: 60);

        var result = _sut.CalculateMacd(candles);

        var lastMacd = result.Last(r => r.MacdLine.HasValue);
        Assert.That(lastMacd.MacdLine!.Value, Is.GreaterThan(0m),
            "In a consistent uptrend, fast EMA > slow EMA, so MACD must be positive");
    }

    [Test]
    [Description("FastEMA < SlowEMA in downtrend → MacdLine < 0")]
    public void CalculateMacd_StrongDowntrend_MacdLineIsNegative()
    {
        var candles = MarketDataFixtures.CreateTrendingCandles(uptrend: false, count: 60);

        var result = _sut.CalculateMacd(candles);

        var lastMacd = result.Last(r => r.MacdLine.HasValue);
        Assert.That(lastMacd.MacdLine!.Value, Is.LessThan(0m),
            "In a consistent downtrend, fast EMA < slow EMA, so MACD must be negative");
    }

    [Test]
    public void CalculateMacd_Histogram_EqualsLine_MinusSignal()
    {
        // Histogram is defined as MacdLine - SignalLine. We verify this invariant
        // holds for every valid (non-null) entry.
        var candles = MarketDataFixtures.CreateCandles(60);

        var result = _sut.CalculateMacd(candles);

        var valid = result.Where(r => r.MacdLine.HasValue && r.SignalLine.HasValue && r.Histogram.HasValue);
        foreach (var entry in valid)
        {
            var expected = entry.MacdLine!.Value - entry.SignalLine!.Value;
            Assert.That((double)entry.Histogram!.Value,
                Is.EqualTo((double)expected).Within(0.0001),
                $"Histogram on {entry.Date:yyyy-MM-dd} violates invariant: MacdLine - SignalLine");
        }
    }
}
