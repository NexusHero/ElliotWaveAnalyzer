namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Configuration for a backtest run: how much history to warm up on before the first count, how far to
/// slide the cutoff each step, how many following candles to score each recorded scenario against, and
/// the pivot reversal threshold. Part of the dataset identity (a run is keyed by dataset + config), so
/// two runs with the same candles and config are the same run.
/// </summary>
/// <param name="WarmupCandles">Minimum candles visible before the first cutoff (need enough for pivots + parse).</param>
/// <param name="Step">Candles the cutoff advances each step.</param>
/// <param name="HorizonCandles">Candles after the cutoff used to score the recorded scenario; 0 = all remaining.</param>
/// <param name="PivotThresholdPercent">ZigZag reversal threshold, in percent, for pivot detection.</param>
/// <param name="Timeframe">Timeframe label recorded on each result (e.g. "1D").</param>
public sealed record BacktestConfig(
    int WarmupCandles = 120,
    int Step = 5,
    int HorizonCandles = 60,
    decimal PivotThresholdPercent = 3m,
    string Timeframe = "1D")
{
    /// <summary>A stable, human-readable canonical form used in the dataset hash.</summary>
    public string Canonical()
        => $"warmup={WarmupCandles};step={Step};horizon={HorizonCandles};thr={PivotThresholdPercent};tf={Timeframe}";
}
