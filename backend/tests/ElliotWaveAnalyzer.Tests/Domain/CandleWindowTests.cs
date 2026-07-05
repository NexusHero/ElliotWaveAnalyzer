using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Tests.TestData;

namespace ElliotWaveAnalyzer.Tests.Domain;

/// <summary>
/// The no-lookahead guard: a <see cref="CandleWindow"/> exposes exactly its cutoff of candles and
/// refuses any access at or beyond it, so the analysis stage physically cannot see the future.
/// </summary>
[TestFixture]
public sealed class CandleWindowTests
{
    private static readonly IReadOnlyList<MarketCandle> Candles = MarketDataFixtures.CreateCandles(10);

    [Test]
    public void Count_IsTheCutoff_NotTheBackingLength()
    {
        var window = new CandleWindow(Candles, cutoff: 4);
        Assert.That(window.Count, Is.EqualTo(4));
    }

    [Test]
    public void Indexer_WithinCutoff_ReturnsTheCandle()
    {
        var window = new CandleWindow(Candles, cutoff: 4);
        Assert.That(window[3], Is.EqualTo(Candles[3]));
    }

    [Test]
    public void Indexer_AtOrBeyondCutoff_Throws()
    {
        var window = new CandleWindow(Candles, cutoff: 4);

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = window[4], "the cutoff candle is the future");
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = window[9]);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = window[-1]);
        });
    }

    [Test]
    public void Enumeration_StopsAtTheCutoff()
    {
        var window = new CandleWindow(Candles, cutoff: 3);
        Assert.That(window.ToList(), Is.EqualTo(Candles.Take(3)));
    }

    [Test]
    public void Constructor_CutoffOutOfRange_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CandleWindow(Candles, cutoff: 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CandleWindow(Candles, cutoff: -1));
        });
    }

    [Test]
    public void Constructor_CutoffAtFullLength_IsAllowed()
        => Assert.That(new CandleWindow(Candles, cutoff: 10).Count, Is.EqualTo(10));
}
