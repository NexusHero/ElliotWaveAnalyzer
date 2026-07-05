using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Tests for the pure <see cref="BacktestEngine"/>. The most important is the <b>no-lookahead</b>
/// property, checked two independent ways: (1) poisoning the post-cutoff candles with a violent
/// reversal must not change any scenario recorded at an earlier cutoff, and (2) results whose full
/// horizon is already available are identical whether computed on the full series or a series
/// truncated right after that horizon. Either would fail if the analysis stage could see the future.
/// </summary>
[TestFixture]
public sealed class BacktestEngineTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly BacktestConfig Config =
        new(WarmupCandles: 36, Step: 6, HorizonCandles: 12, PivotThresholdPercent: 3m);

    /// <summary>
    /// Builds a clean, repeating up-impulse as a piecewise-linear path through turning points, so the
    /// ZigZag detector lands pivots on the turns and the parser finds rule-valid counts at many cutoffs.
    /// </summary>
    private static IReadOnlyList<MarketCandle> ImpulseSeries()
    {
        // One up-impulse (1-2-3-4-5), then drift up and repeat, four times.
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

    private static (string Structure, bool Bullish, string Conf, string Confl) Key(BacktestScenarioResult r)
        => (r.Structure, r.Bullish, r.ConfidenceBucket, r.ConfluenceBucket);

    [Test]
    public void Run_OnACleanImpulseSeries_RecordsScenarios()
    {
        var results = BacktestEngine.Run(ImpulseSeries(), Config);
        Assert.That(results, Is.Not.Empty, "a clean impulse series should yield rule-valid counts");
    }

    [Test]
    public void Run_PoisonedPostCutoffCandles_DoNotChangeEarlierRecordedScenarios()
    {
        var clean = ImpulseSeries();
        const int k = 120; // everything up to here is shared

        // Poison: identical first k candles, then a violent crash that WOULD flip the count if leaked.
        var poisoned = new List<MarketCandle>(clean.Take(k));
        var last = poisoned[^1];
        for (var i = 0; i < 40; i++)
        {
            var price = last.Close * (1m - (0.05m * (i + 1)));
            price = Math.Max(1m, price);
            poisoned.Add(new MarketCandle(last.OpenTime.AddDays(i + 1), last.Close, last.Close, price, price, 0m));
        }

        var cleanByDate = BacktestEngine.Run(clean, Config).ToDictionary(r => r.CutoffDate);
        var poisonByDate = BacktestEngine.Run(poisoned, Config).ToDictionary(r => r.CutoffDate);

        var sharedCutoffDate = clean[k - 1].OpenTime;
        var compared = 0;
        foreach (var (date, cleanResult) in cleanByDate)
        {
            if (date > sharedCutoffDate || !poisonByDate.TryGetValue(date, out var poisonResult))
            {
                continue; // only cutoffs whose entire visible window is shared
            }

            // The recorded scenario (geometry-derived) must be byte-identical; only the future-scored
            // Outcome may differ, and we deliberately do not compare it.
            Assert.That(Key(poisonResult), Is.EqualTo(Key(cleanResult)),
                $"scenario at cutoff {date:o} changed when the future was poisoned — lookahead leak");
            compared++;
        }

        Assert.That(compared, Is.GreaterThan(0), "the test must actually compare shared cutoffs");
    }

    [Test]
    public void Run_ResultsWithFullHorizon_AreIdenticalOnTruncatedSeries()
    {
        var full = ImpulseSeries();
        const int truncateAt = 150;
        var truncated = full.Take(truncateAt).ToList();

        var fullResults = BacktestEngine.Run(full, Config);
        var truncatedResults = BacktestEngine.Run(truncated, Config);
        var truncatedByDate = truncatedResults.ToDictionary(r => r.CutoffDate);

        // For a cutoff whose full horizon (cutoff + HorizonCandles) is within the truncation point,
        // the full and truncated runs must agree on EVERYTHING, including the scored outcome.
        var compared = 0;
        foreach (var r in fullResults)
        {
            var cutoffIndex = IndexOf(full, r.CutoffDate) + 1;
            if (cutoffIndex + Config.HorizonCandles > truncateAt)
            {
                continue;
            }

            Assert.That(truncatedByDate[r.CutoffDate], Is.EqualTo(r),
                "a fully-scored result changed when later candles were removed — lookahead leak");
            compared++;
        }

        Assert.That(compared, Is.GreaterThan(0));
    }

    [Test]
    public void Run_IsDeterministic_SameInputSameResults()
    {
        var candles = ImpulseSeries();
        Assert.That(BacktestEngine.Run(candles, Config), Is.EqualTo(BacktestEngine.Run(candles, Config)));
    }

    [Test]
    public void Run_Cancellation_StopsTheRun()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => BacktestEngine.Run(ImpulseSeries(), Config, cts.Token));
    }

    private static int IndexOf(IReadOnlyList<MarketCandle> candles, DateTime date)
    {
        for (var i = 0; i < candles.Count; i++)
        {
            if (candles[i].OpenTime == date)
            {
                return i;
            }
        }

        return -1;
    }
}
