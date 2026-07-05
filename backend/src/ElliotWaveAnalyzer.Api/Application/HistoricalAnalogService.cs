using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Orchestrates the historical-analog read: fetch the symbol's candles, build the no-lookahead corpus
/// of its past setups (<see cref="SetupHistoryBuilder"/>), fingerprint the <em>current</em> count the
/// same way, retrieve + aggregate the nearest concluded analogs (<see cref="HistoricalAnalogReporter"/>),
/// and hand the deterministic report to the narrator for an optional grounded summary. The momentum
/// regimes are computed once over the full series and read at each point — RSI/MACD are causal, so
/// reading them at a cutoff is no lookahead. No geometry or statistic is ever produced by the LLM.
/// <para>
/// The corpus sweep replays the parser at many cutoffs, so it is cost-heavy; the deterministic report
/// is cached per (symbol, timeframe, day) and only the (cheap) narration runs on a cache hit.
/// </para>
/// </summary>
public sealed class HistoricalAnalogService(
    IEnumerable<IMarketDataProvider> providers,
    IIndicatorCalculator indicators,
    IAnalogNarrator narrator,
    IMemoryCache cache) : IHistoricalAnalogService
{
    // Enough history for a meaningful corpus while keeping the sweep (and each cutoff's parse) bounded.
    private const int LookbackDays = 600;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(6);

    private readonly IReadOnlyList<IMarketDataProvider> _providers = [.. providers];

    /// <inheritdoc/>
    public async Task<AnalogReport?> AnalyzeAsync(
        string symbol,
        CandleInterval interval,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);

        var key = $"analogs:{symbol}:{interval}:{DateTime.UtcNow:yyyyMMdd}";
        if (!cache.TryGetValue(key, out AnalogReport? report))
        {
            report = await BuildDeterministicReportAsync(symbol, interval, cancellationToken);
            cache.Set(key, report, CacheTtl); // cache "no data" (null) too, so it isn't recomputed all day
        }

        return report is null ? null : await narrator.NarrateAsync(report, cancellationToken);
    }

    // The heavy, deterministic half — cached. Fetch candles, build the corpus, fingerprint the current
    // count, and compose the report (no narrative).
    private async Task<AnalogReport?> BuildDeterministicReportAsync(
        string symbol, CandleInterval interval, CancellationToken cancellationToken)
    {
        var provider = _providers.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new ArgumentException($"No market data provider supports symbol '{symbol}'.", nameof(symbol));

        var daily = await provider.GetCandlesAsync(symbol, LookbackDays, cancellationToken);
        var candles = CandleResampler.Resample(daily, interval);

        var config = ConfigFor(interval);
        if (candles.Count <= config.WarmupCandles)
        {
            return null; // not enough history to form even one past setup
        }

        var rsi = indicators.CalculateRsi(candles);
        var macd = indicators.CalculateMacd(candles);
        (double Rsi, double Macd) Momentum(CandleWindow window) => RegimeAt(rsi, macd, window.Count - 1);

        var corpus = SetupHistoryBuilder.Build(symbol, candles, config, Momentum, cancellationToken);

        var query = ExtractCurrent(candles, config, rsi, macd, cancellationToken);
        if (query is null)
        {
            return null; // no current rule-valid count to compare
        }

        var asOf = new DateTimeOffset(DateTime.SpecifyKind(candles[^1].OpenTime, DateTimeKind.Utc));
        return HistoricalAnalogReporter.Report(query, corpus, asOf);
    }

    // The current count's fingerprint: run the pipeline on the full (untruncated) series.
    private static SetupFeatures? ExtractCurrent(
        IReadOnlyList<MarketCandle> candles,
        BacktestConfig config,
        IReadOnlyList<RsiResult> rsi,
        IReadOnlyList<MacdResult> macd,
        CancellationToken cancellationToken)
    {
        var pivots = SwingPivotDetector.Detect(candles, config.PivotThresholdPercent);
        var (candidates, _) = WaveCandidateGenerator.GenerateParsed(pivots, cancellationToken: cancellationToken);
        if (candidates.Count == 0 || candidates[0].Levels is not { } levels) return null;
        if (!Enum.TryParse<StructureKind>(candidates[0].Structure, out var structure)) return null;

        var last = candles.Count - 1;
        var (rsiRegime, macdRegime) = RegimeAt(rsi, macd, last);
        return SetupFeatureExtractor.Extract(
            structure, levels, candidates[0].Score ?? 0m, candles[last].Close, rsiRegime, macdRegime, config.Timeframe);
    }

    // RSI/MACD → [0, 1] regimes. RSI is level/100; MACD uses the histogram sign (price-scale-invariant).
    // Warm-up nulls fall back to the neutral 0.5, so an early window does not skew similarity.
    private static (double Rsi, double Macd) RegimeAt(
        IReadOnlyList<RsiResult> rsi, IReadOnlyList<MacdResult> macd, int index)
    {
        var rsiRegime = index >= 0 && index < rsi.Count && rsi[index].Value is { } r
            ? Math.Clamp((double)r / 100.0, 0.0, 1.0)
            : 0.5;

        var macdRegime = 0.5;
        if (index >= 0 && index < macd.Count && macd[index].Histogram is { } h)
        {
            macdRegime = h > 0 ? 0.75 : h < 0 ? 0.25 : 0.5;
        }

        return (rsiRegime, macdRegime);
    }

    // A coarser sweep than the backtest (wider step, shorter warm-up) keeps the on-demand build fast
    // while still yielding a usable corpus; daily/weekly differ only in the recorded timeframe label.
    private static BacktestConfig ConfigFor(CandleInterval interval) =>
        new(WarmupCandles: 90, Step: 15, HorizonCandles: 60,
            Timeframe: interval == CandleInterval.OneWeek ? "1W" : "1D");
}
