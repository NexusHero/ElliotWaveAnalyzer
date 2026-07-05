using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Builds the historical-analog corpus for one instrument by replaying the deterministic pipeline over
/// its candle history with <b>no lookahead</b> — the same sweep the backtest uses (<see cref="BacktestEngine"/>,
/// ADR-027). At each cutoff the analysis stage sees only a <see cref="CandleWindow"/>; the formed count
/// is fingerprinted with <see cref="SetupFeatureExtractor"/>, and the candles that <i>follow</i> the
/// cutoff score its outcome with the existing <see cref="AnalysisOutcomeEvaluator"/>. Each row therefore
/// records where the setup formed, how it actually resolved and when — exactly what the retriever needs,
/// and structurally impossible to leak the future. The momentum regimes are supplied by a delegate
/// (the indicator calculator in production, a stub in tests) so this stays a pure function.
/// </summary>
public static class SetupHistoryBuilder
{
    /// <summary>
    /// Sweeps <paramref name="candles"/> (ascending by time) into the corpus of concluded and pending
    /// historical setups for <paramref name="symbol"/>. A window with no rule-valid count is skipped.
    /// </summary>
    /// <param name="momentum">Maps the cutoff window to its (RSI, MACD) regimes in [0, 1].</param>
    public static IReadOnlyList<HistoricalSetup> Build(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        BacktestConfig config,
        Func<CandleWindow, (double Rsi, double Macd)> momentum,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(candles);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(momentum);

        var setups = new List<HistoricalSetup>();
        var step = Math.Max(1, config.Step);

        for (var cutoff = config.WarmupCandles; cutoff < candles.Count; cutoff += step)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var window = new CandleWindow(candles, cutoff);
            var pivots = SwingPivotDetector.Detect(window, config.PivotThresholdPercent);
            var (candidates, _) = WaveCandidateGenerator.GenerateParsed(
                pivots, cancellationToken: cancellationToken);
            if (candidates.Count == 0 || candidates[0].Levels is not { } levels) continue;
            if (!Enum.TryParse<StructureKind>(candidates[0].Structure, out var structure)) continue;

            var currentPrice = window[cutoff - 1].Close;
            var (rsi, macd) = momentum(window);
            var features = SetupFeatureExtractor.Extract(
                structure, levels, candidates[0].Score ?? 0m, currentPrice, rsi, macd, config.Timeframe);

            var horizonEnd = config.HorizonCandles > 0
                ? Math.Min(cutoff + config.HorizonCandles, candles.Count)
                : candles.Count;
            var after = Slice(candles, cutoff, horizonEnd);
            var target = levels.TargetZones.Count > 0 ? levels.TargetZones[0] : null;

            var eval = AnalysisOutcomeEvaluator.Evaluate(
                levels.Bullish,
                levels.Invalidation?.Price,
                levels.Invalidation?.Side == LevelSide.Above,
                target?.Low,
                target?.High,
                after);

            var formedAt = new DateTimeOffset(
                DateTime.SpecifyKind(window[cutoff - 1].OpenTime, DateTimeKind.Utc));

            setups.Add(new HistoricalSetup(symbol, formedAt, eval.At, eval.Outcome, features));
        }

        return setups;
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
}
