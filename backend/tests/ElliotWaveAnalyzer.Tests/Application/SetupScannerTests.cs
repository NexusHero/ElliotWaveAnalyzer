using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The pure scanner: a clean impulse series yields a hit, a flat series yields none, the filter narrows
/// exactly, and ranking puts in-zone/high-score/tight-risk first.
/// </summary>
[TestFixture]
public sealed class SetupScannerTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A clean, repeating up-impulse (piecewise-linear through turning points).</summary>
    private static IReadOnlyList<MarketCandle> ImpulseSeries()
    {
        decimal[] block = [100, 130, 115, 175, 150, 200];
        var turns = new List<decimal>();
        decimal lift = 0m;
        for (var b = 0; b < 4; b++)
        {
            foreach (var p in block)
            {
                turns.Add(p + lift);
            }

            lift += 120m;
        }

        var candles = new List<MarketCandle>();
        var day = 0;
        for (var i = 0; i + 1 < turns.Count; i++)
        {
            for (var s = 0; s < 6; s++)
            {
                var t = (decimal)(s + 1) / 6;
                var price = turns[i] + ((turns[i + 1] - turns[i]) * t);
                var prev = candles.Count > 0 ? candles[^1].Close : turns[0];
                candles.Add(new MarketCandle(
                    Start.AddDays(day++), prev, Math.Max(prev, price), Math.Min(prev, price), price, 0m));
            }
        }

        return candles;
    }

    [Test]
    public void Scan_CleanImpulse_ReturnsAHit()
    {
        var hit = SetupScanner.Scan("BTC", ImpulseSeries());

        Assert.Multiple(() =>
        {
            Assert.That(hit, Is.Not.Null);
            Assert.That(hit!.Symbol, Is.EqualTo("BTC"));
            Assert.That(hit.Structure, Is.Not.Empty);
            Assert.That(hit.UnfoldingWave, Is.Not.Empty);
            Assert.That(hit.CurrentPrice, Is.GreaterThan(0m));
        });
    }

    [Test]
    public void Scan_FlatSeries_ReturnsNull()
    {
        var flat = Enumerable.Range(0, 50)
            .Select(i => new MarketCandle(Start.AddDays(i), 100m, 100m, 100m, 100m, 0m))
            .ToList();

        Assert.That(SetupScanner.Scan("FLAT", flat), Is.Null);
    }

    [Test]
    public void Scan_EmptyCandles_ReturnsNull()
        => Assert.That(SetupScanner.Scan("X", []), Is.Null);

    [Test]
    public void Rank_OrdersInZoneThenScoreThenTightestRisk()
    {
        var a = Hit("AAA", score: 0.5m, inZone: false, dist: 5m);
        var b = Hit("BBB", score: 0.9m, inZone: false, dist: 20m);
        var c = Hit("CCC", score: 0.4m, inZone: true, dist: 30m);

        var ranked = SetupScanner.Rank([a, b, c]);

        Assert.Multiple(() =>
        {
            Assert.That(ranked[0].Symbol, Is.EqualTo("CCC"), "in-zone wins");
            Assert.That(ranked[1].Symbol, Is.EqualTo("BBB"), "then higher score");
            Assert.That(ranked[2].Symbol, Is.EqualTo("AAA"));
        });
    }

    [Test]
    public void Filter_NarrowsByStructureScoreAndZone()
    {
        var impulse = Hit("I", score: 0.8m, inZone: true, dist: 5m, structure: "Impulse");

        Assert.Multiple(() =>
        {
            Assert.That(new ScanFilter(Structure: "Impulse").Matches(impulse), Is.True);
            Assert.That(new ScanFilter(Structure: "Zigzag").Matches(impulse), Is.False);
            Assert.That(new ScanFilter(MinScore: 0.9m).Matches(impulse), Is.False);
            Assert.That(new ScanFilter(MinScore: 0.7m).Matches(impulse), Is.True);
            Assert.That(new ScanFilter(InZoneOnly: true).Matches(impulse), Is.True);
            Assert.That(new ScanFilter(InZoneOnly: true).Matches(impulse with { InEntryZone = false, InConfluenceZone = false }), Is.False);
        });
    }

    private static ScanHit Hit(
        string symbol, decimal score, bool inZone, decimal dist, string structure = "Impulse") =>
        new(symbol, structure, "Wave 3", true, score, 100m, 90m, dist, inZone, InConfluenceZone: false);
}
