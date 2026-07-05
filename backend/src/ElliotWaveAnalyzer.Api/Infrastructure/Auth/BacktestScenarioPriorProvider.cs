using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Reads the latest backtest run's "confidence"-dimension buckets from <see cref="AppDbContext"/> and
/// exposes their hit-rates as priors, so a saved scenario can be given an honest probability before
/// the user's own track record is large enough to calibrate one (REQ-026). Only buckets that actually
/// concluded scenarios contribute a prior.
/// </summary>
internal sealed class BacktestScenarioPriorProvider(AppDbContext db) : IScenarioPriorProvider
{
    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, decimal>> GetConfidencePriorsAsync(
        CancellationToken cancellationToken = default)
    {
        var run = await db.BacktestRuns
            .AsNoTracking()
            .Include(r => r.Buckets)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return new Dictionary<string, decimal>();
        }

        return run.Buckets
            .Where(b => b is { Dimension: "confidence", Concluded: > 0 })
            .ToDictionary(
                b => b.Key,
                b => (decimal)b.TargetReached / b.Concluded,
                StringComparer.OrdinalIgnoreCase);
    }
}
