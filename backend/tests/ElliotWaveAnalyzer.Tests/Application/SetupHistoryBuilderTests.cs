using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The corpus sweep: it produces fingerprinted setups from a clean impulse history, applies the
/// momentum delegate, is deterministic, and — the load-bearing guarantee — is <b>no-lookahead</b>:
/// poisoning the candles after a cutoff cannot change the <em>features</em> of a setup formed at or
/// before it (only its later-scored outcome, which legitimately depends on the future).
/// </summary>
[TestFixture]
public sealed class SetupHistoryBuilderTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly BacktestConfig Config =
        new(WarmupCandles: 36, Step: 6, HorizonCandles: 12, PivotThresholdPercent: 3m);

    private static (double, double) NeutralMomentum(CandleWindow _) => (0.5, 0.5);

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

        return BuildPath(turns, perLeg: 6);
    }

    private static IReadOnlyList<MarketCandle> BuildPath(IReadOnlyList<decimal> turns, int perLeg)
    {
        var candles = new List<MarketCandle>();
        var day = 0;
        for (var i = 0; i + 1 < turns.Count; i++)
        {
            for (var s = 0; s < perLeg; s++)
            {
                var t = (decimal)(s + 1) / perLeg;
                var price = turns[i] + ((turns[i + 1] - turns[i]) * t);
                var prev = candles.Count > 0 ? candles[^1].Close : turns[0];
                candles.Add(new MarketCandle(
                    Start.AddDays(day++), prev, Math.Max(prev, price), Math.Min(prev, price), price, 0m));
            }
        }

        return candles;
    }

    [Test]
    public void Build_OnImpulseSeries_ProducesSetups()
    {
        var setups = SetupHistoryBuilder.Build("BTCUSD", ImpulseSeries(), Config, NeutralMomentum);

        Assert.Multiple(() =>
        {
            Assert.That(setups, Is.Not.Empty);
            Assert.That(setups.All(s => s.Symbol == "BTCUSD"), Is.True);
            Assert.That(setups.All(s => s.Features.Timeframe == Config.Timeframe), Is.True);
        });
    }

    [Test]
    public void Build_AppliesTheMomentumDelegate()
    {
        var setups = SetupHistoryBuilder.Build("BTCUSD", ImpulseSeries(), Config, _ => (0.3, 0.7));

        Assert.Multiple(() =>
        {
            Assert.That(setups.All(s => s.Features.RsiRegime == 0.3), Is.True);
            Assert.That(setups.All(s => s.Features.MacdRegime == 0.7), Is.True);
        });
    }

    [Test]
    public void Build_IsDeterministic()
    {
        var series = ImpulseSeries();
        var first = SetupHistoryBuilder.Build("BTCUSD", series, Config, NeutralMomentum);
        var second = SetupHistoryBuilder.Build("BTCUSD", series, Config, NeutralMomentum);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Build_NoLookahead_PoisonedFutureDoesNotChangeEarlierSetupFeatures()
    {
        var clean = ImpulseSeries();
        const int poisonAt = 120;
        var poisonDate = clean[poisonAt].OpenTime;

        // Same first `poisonAt` candles, then a violent crash that would flip any leaking count.
        var poisoned = new List<MarketCandle>(clean.Take(poisonAt));
        var last = poisoned[^1].Close;
        for (var i = poisonAt; i < clean.Count; i++)
        {
            last -= 20m;
            var price = Math.Max(1m, last);
            poisoned.Add(new MarketCandle(clean[i].OpenTime, price, price, price, price, 0m));
        }

        var cleanSetups = SetupHistoryBuilder.Build("SYM", clean, Config, NeutralMomentum);
        var poisonedSetups = SetupHistoryBuilder.Build("SYM", poisoned, Config, NeutralMomentum);

        // For every setup formed before the poison point, the FEATURES must be byte-identical — the
        // count that produced them could not have seen the poisoned future.
        var cleanByDate = cleanSetups.ToDictionary(s => s.FormedAt);
        foreach (var poisonedSetup in poisonedSetups.Where(s => s.FormedAt < poisonDate))
        {
            Assert.That(cleanByDate.TryGetValue(poisonedSetup.FormedAt, out var cleanSetup), Is.True);
            Assert.That(poisonedSetup.Features, Is.EqualTo(cleanSetup!.Features),
                $"features at {poisonedSetup.FormedAt:d} changed under a poisoned future — lookahead leak");
        }
    }
}
