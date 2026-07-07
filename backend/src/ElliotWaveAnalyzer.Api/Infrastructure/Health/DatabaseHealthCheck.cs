using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Health;

/// <summary>
/// Readiness check for the database (#173 AC1, AC4): unhealthy when unreachable, and — the part a
/// bare connectivity ping misses — also unhealthy while migrations are still pending, so a load
/// balancer never routes traffic to an instance whose schema isn't actually ready yet.
/// </summary>
internal sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Database is unreachable.");
            }

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any()
                ? HealthCheckResult.Unhealthy($"{pending.Count()} pending migration(s).")
                : HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database is unreachable.", ex);
        }
    }
}
