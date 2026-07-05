namespace ElliotWaveAnalyzer.Api.Endpoints;

/// <summary>
/// Body of <c>POST /api/backtest/run</c>. Only <see cref="Symbol"/> is required; the rest override the
/// <see cref="Domain.BacktestConfig"/> defaults when supplied.
/// </summary>
/// <param name="Symbol">Instrument to backtest (must be supported by a market-data provider).</param>
/// <param name="WarmupCandles">Candles visible before the first cutoff.</param>
/// <param name="Step">Candles the cutoff advances each step.</param>
/// <param name="HorizonCandles">Candles after the cutoff used to score each scenario (0 = all remaining).</param>
/// <param name="PivotThresholdPercent">ZigZag reversal threshold, in percent.</param>
/// <param name="Timeframe">Timeframe label recorded on each result.</param>
public sealed record RunBacktestRequest(
    string Symbol,
    int? WarmupCandles = null,
    int? Step = null,
    int? HorizonCandles = null,
    decimal? PivotThresholdPercent = null,
    string? Timeframe = null);
