using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure orchestration of the existing deterministic pipeline over history, measuring scenario hit
/// rates with <b>no lookahead</b>. It slides a cutoff forward; at each step the analysis stage
/// (pivots → parse → best count → levels) sees only a <see cref="CandleWindow"/> bounded at the
/// cutoff, and the following candles are used <i>solely</i> to score the recorded scenario with the
/// existing <see cref="AnalysisOutcomeEvaluator"/> semantics. No LLM, no I/O — deterministic given a
/// candle series and a <see cref="BacktestConfig"/> (the credibility engine, issue #121).
/// </summary>
public static class BacktestEngine
{
    /// <summary>Engine version, recorded on each run so results across versions stay distinguishable.</summary>
    public const string EngineVersion = "1";

    /// <summary>
    /// Runs the backtest over <paramref name="candles"/> (ascending by time). Returns one
    /// <see cref="BacktestScenarioResult"/> per cutoff at which a rule-valid count existed. A window
    /// with no valid count is skipped (not an error).
    /// </summary>
    public static IReadOnlyList<BacktestScenarioResult> Run(
        IReadOnlyList<MarketCandle> candles,
        BacktestConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(config);

        var results = new List<BacktestScenarioResult>();
        var step = Math.Max(1, config.Step);

        for (var cutoff = config.WarmupCandles; cutoff < candles.Count; cutoff += step)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The analysis stage sees ONLY these candles — the guard type forbids reaching past cutoff.
            var window = new CandleWindow(candles, cutoff);
            var scenario = RecordScenario(window, config, cancellationToken);
            if (scenario is null)
            {
                continue;
            }

            // Score against the candles AFTER the cutoff (index cutoff onward), bounded by the horizon.
            var horizonEnd = config.HorizonCandles > 0
                ? Math.Min(cutoff + config.HorizonCandles, candles.Count)
                : candles.Count;
            var after = Slice(candles, cutoff, horizonEnd);

            var eval = AnalysisOutcomeEvaluator.Evaluate(
                scenario.Bullish,
                scenario.InvalidationPrice,
                scenario.InvalidationAbove,
                scenario.TargetLow,
                scenario.TargetHigh,
                after);

            results.Add(new BacktestScenarioResult(
                window[cutoff - 1].OpenTime,
                scenario.Structure,
                config.Timeframe,
                scenario.ConfidenceBucket,
                scenario.ConfluenceBucket,
                scenario.Bullish,
                eval.Outcome));
        }

        return results;
    }

    /// <summary>
    /// Runs the deterministic pipeline on the cutoff-bounded window and extracts the best count's
    /// scored geometry, or null when no rule-valid count with usable levels exists yet.
    /// </summary>
    private static RecordedScenario? RecordScenario(
        CandleWindow window, BacktestConfig config, CancellationToken cancellationToken)
    {
        var pivots = SwingPivotDetector.Detect(window, config.PivotThresholdPercent);
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(
            pivots, cancellationToken: cancellationToken);
        if (candidates.Count == 0 || candidates[0].Levels is not { } levels)
        {
            return null;
        }

        var best = candidates[0];
        var target = levels.TargetZones.Count > 0 ? levels.TargetZones[0] : null;

        return new RecordedScenario(
            best.Structure,
            levels.Bullish,
            levels.Invalidation?.Price,
            levels.Invalidation?.Side == LevelSide.Above,
            target?.Low,
            target?.High,
            ConfidenceBucket(best.Score),
            ConfluenceBucket(levels.ConfluenceZones));
    }

    /// <summary>Score → confidence bucket, mirroring the "high/medium/low" the live calibration uses.</summary>
    private static string ConfidenceBucket(decimal? score) => score switch
    {
        >= 0.66m => "high",
        >= 0.4m => "medium",
        _ => "low",
    };

    /// <summary>Strength of the strongest confluence zone: none / weak / strong.</summary>
    private static string ConfluenceBucket(IReadOnlyList<ConfluenceZone> zones)
    {
        if (zones.Count == 0)
        {
            return "none";
        }

        // "Strong" = the top zone stacks at least two contributing Fibonacci levels (multi-ratio
        // agreement); a lone level is "weak". Robust to the absolute score scale.
        var top = zones.MaxBy(z => z.Score)!;
        return top.Contributions.Count >= 2 ? "strong" : "weak";
    }

    private static IReadOnlyList<MarketCandle> Slice(IReadOnlyList<MarketCandle> candles, int start, int end)
    {
        var slice = new List<MarketCandle>(Math.Max(0, end - start));
        for (var i = start; i < end; i++)
        {
            slice.Add(candles[i]);
        }

        return slice;
    }

    private sealed record RecordedScenario(
        string Structure,
        bool Bullish,
        decimal? InvalidationPrice,
        bool InvalidationAbove,
        decimal? TargetLow,
        decimal? TargetHigh,
        string ConfidenceBucket,
        string ConfluenceBucket);
}
