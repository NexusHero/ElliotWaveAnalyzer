using CsCheck;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Property-based no-lookahead invariant (I3), the general form of the backtest guarantee: for any
/// generated series, any cutoff, and any arbitrary/poisoned future appended beyond it, the deterministic
/// analysis over the <see cref="CandleWindow"/> is <b>identical</b> to the analysis over only the visible
/// candles — the future physically cannot leak, however it is chosen.
/// </summary>
[TestFixture]
public sealed class NoLookaheadProperties
{
    // A base series, an independently generated "future" (used to poison the tail), and a raw cutoff.
    private static readonly Gen<(MarketCandle[] baseSeries, MarketCandle[] future, int cutRaw)> Inputs =
        Gen.Select(PropertyGenerators.Candles, PropertyGenerators.Candles, Gen.Int[0, 1000]);

    [Test]
    public void WindowedPivots_AreUnaffectedByAnyAppendedFuture()
    {
        Inputs.Sample(x =>
        {
            var (baseSeries, future, cutRaw) = x;
            var cutoff = cutRaw % (baseSeries.Length + 1); // in [0, len]

            var visible = baseSeries.Take(cutoff).ToList();

            // A poisoned series: the visible candles, then arbitrary future candles re-dated to follow on.
            var poisoned = visible
                .Concat(future.Select((f, i) => f with { OpenTime = PropertyGenerators.T0.AddDays(cutoff + i) }))
                .ToList();

            var windowed = SwingPivotDetector.Detect(new CandleWindow(poisoned, cutoff));
            var visibleOnly = SwingPivotDetector.Detect(visible);

            Assert.That(windowed, Is.EqualTo(visibleOnly));
        });
    }

    [Test]
    public void Window_ExposesExactlyTheVisibleCandles_RegardlessOfThePoisonedTail()
    {
        Inputs.Sample(x =>
        {
            var (baseSeries, future, cutRaw) = x;
            var cutoff = cutRaw % (baseSeries.Length + 1);

            var visible = baseSeries.Take(cutoff).ToList();
            var poisoned = visible
                .Concat(future.Select((f, i) => f with { OpenTime = PropertyGenerators.T0.AddDays(cutoff + i) }))
                .ToList();

            var window = new CandleWindow(poisoned, cutoff);

            Assert.That(window.Count, Is.EqualTo(cutoff));
            Assert.That(window.ToList(), Is.EqualTo(visible));
        });
    }

    [Test]
    public void Window_RefusesAnyAccessAtOrBeyondTheCutoff()
    {
        Gen.Select(PropertyGenerators.Candles, Gen.Int[0, 1000]).Sample(x =>
        {
            var (series, cutRaw) = x;
            var cutoff = cutRaw % (series.Length + 1);
            var window = new CandleWindow(series, cutoff);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ = window[cutoff]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = window[-1]);
        });
    }
}
