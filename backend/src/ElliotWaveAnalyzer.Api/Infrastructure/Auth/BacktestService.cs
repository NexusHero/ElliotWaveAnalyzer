using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Runs the pure <see cref="BacktestEngine"/> over an instrument's history and persists the aggregated
/// result on the shared <see cref="AppDbContext"/>. Idempotent by dataset hash: a re-run over the same
/// candles + config finds the stored run and returns it instead of duplicating rows. Lives in
/// Infrastructure because it fetches market data and touches EF; the engine/aggregation stay pure.
/// </summary>
internal sealed class BacktestService(
    AppDbContext db,
    IEnumerable<IMarketDataProvider> marketDataProviders,
    TimeProvider timeProvider) : IBacktestService
{
    private readonly IReadOnlyList<IMarketDataProvider> _marketDataProviders = [.. marketDataProviders];

    /// <summary>History window to pull for a backtest (clamped by the provider).</summary>
    private const int HistoryDays = 1825;

    /// <inheritdoc/>
    public async Task<BacktestSummary> RunAsync(
        string symbol, BacktestConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        ArgumentNullException.ThrowIfNull(config);

        var provider = _marketDataProviders.FirstOrDefault(p => p.Supports(symbol))
            ?? throw new InvalidOperationException($"No market-data provider supports '{symbol}'.");
        var candles = await provider.GetCandlesAsync(symbol, HistoryDays, cancellationToken);

        var hash = BacktestDataset.Hash(candles, config);
        var existing = await db.BacktestRuns
            .AsNoTracking()
            .Include(r => r.Buckets)
            .FirstOrDefaultAsync(r => r.DatasetHash == hash, cancellationToken);
        if (existing is not null)
        {
            return ToSummary(existing); // idempotent — same dataset, same run
        }

        var results = BacktestEngine.Run(candles, config, cancellationToken);
        var buckets = BacktestAggregator.Aggregate(results);

        var run = new BacktestRun
        {
            Id = Guid.NewGuid(),
            DatasetHash = hash,
            EngineVersion = BacktestEngine.EngineVersion,
            Symbol = symbol.ToUpperInvariant(),
            Config = config.Canonical(),
            ScenarioCount = results.Count,
            CreatedAt = timeProvider.GetUtcNow(),
            Buckets = [.. buckets.Select(b => new BacktestBucketRow
            {
                Id = Guid.NewGuid(),
                Dimension = b.Dimension,
                Key = b.Key,
                Total = b.Total,
                Concluded = b.Concluded,
                TargetReached = b.TargetReached,
                Invalidated = b.Invalidated,
            })],
        };

        db.BacktestRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(run);
    }

    /// <inheritdoc/>
    public async Task<BacktestSummary?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var run = await db.BacktestRuns
            .AsNoTracking()
            .Include(r => r.Buckets)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return run is null ? null : ToSummary(run);
    }

    private static BacktestSummary ToSummary(BacktestRun run) => new(
        run.DatasetHash,
        run.EngineVersion,
        run.Symbol,
        run.ScenarioCount,
        run.CreatedAt,
        [.. run.Buckets
            .OrderBy(b => b.Dimension, StringComparer.Ordinal)
            .ThenBy(b => b.Key, StringComparer.Ordinal)
            .Select(b => new BacktestBucket(
                b.Dimension, b.Key, b.Total, b.Concluded, b.TargetReached, b.Invalidated))]);
}
