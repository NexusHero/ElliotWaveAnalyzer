using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Runs the backtest harness over an instrument's history and persists the aggregated hit rates, and
/// reads back the latest run. A run is idempotent: the same candles + config resolve to the same
/// dataset hash, so re-running returns the stored run instead of duplicating it.
/// </summary>
public interface IBacktestService
{
    /// <summary>
    /// Runs a backtest over <paramref name="symbol"/>'s history with <paramref name="config"/>,
    /// persisting the result (or returning the existing run when the dataset hash already exists).
    /// </summary>
    Task<BacktestSummary> RunAsync(string symbol, BacktestConfig config, CancellationToken cancellationToken = default);

    /// <summary>The most recently created run's summary, or null when none has been run yet.</summary>
    Task<BacktestSummary?> GetLatestAsync(CancellationToken cancellationToken = default);
}
