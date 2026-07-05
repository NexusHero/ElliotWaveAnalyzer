using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Wires the pure <see cref="TopDownWaveAnalyzer"/> to real market data: for each rung of a fixed
/// weekly→daily→4-hour ladder it fetches candles (reusing <see cref="ITechnicalAnalysisService"/>,
/// so provider selection and resampling are not duplicated), detects swing pivots, and hands the
/// per-timeframe pivots to the analyzer. A timeframe an instrument cannot serve (e.g. no intraday
/// source for 4H) is skipped honestly rather than failing the whole analysis — the chain simply
/// has one fewer link.
/// </summary>
public sealed class TopDownAnalysisService(
    ITechnicalAnalysisService technicalAnalysis,
    ILogger<TopDownAnalysisService> logger) : ITopDownAnalysisService
{
    // Coarsest → finest. Lookbacks are chosen to yield enough swings per timeframe while keeping
    // 4H inside a typical intraday-history window.
    private static readonly IReadOnlyList<TimeframeRung> Ladder =
    [
        new("1W", CandleInterval.OneWeek, 1825),
        new("1D", CandleInterval.OneDay, 400),
        new("4H", CandleInterval.FourHours, 60),
    ];

    /// <inheritdoc/>
    public async Task<TopDownAnalysis> AnalyzeAsync(
        string symbol, decimal thresholdPercent, CancellationToken cancellationToken = default)
    {
        var timeframes = new List<TimeframePivots>(Ladder.Count);

        foreach (var rung in Ladder)
        {
            try
            {
                var analysis = await technicalAnalysis.GetAnalysisAsync(
                    symbol, rung.LookbackDays, rung.Interval, cancellationToken);
                var pivots = SwingPivotDetector.Detect(analysis.Candles, thresholdPercent);
                timeframes.Add(new TimeframePivots(rung.Label, pivots));
            }
            catch (Exception ex) when (ex is ArgumentException or MarketDataRangeException)
            {
                // This instrument can't serve this timeframe (e.g. no intraday source, or history
                // shorter than the requested window). Skip it — the top-down chain degrades to the
                // timeframes that are available rather than failing outright.
                logger.LogInformation(
                    "Top-down: skipping {Interval} for {Symbol} — {Reason}", rung.Label, symbol, ex.Message);
            }
        }

        if (timeframes.Count == 0)
        {
            throw new ArgumentException(
                $"No timeframe could be analyzed for symbol '{symbol}'.", nameof(symbol));
        }

        return TopDownWaveAnalyzer.Analyze(timeframes, options: null, cancellationToken);
    }

    private readonly record struct TimeframeRung(string Label, CandleInterval Interval, int LookbackDays);
}
