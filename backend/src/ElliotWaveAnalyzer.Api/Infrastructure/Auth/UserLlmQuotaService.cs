using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Infrastructure.Llm;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// <see cref="IUserLlmQuotaService"/> backed by <see cref="AppDbContext"/> (#174). The check-and-consume
/// step is a single atomic conditional <c>UPDATE ... WHERE CallCount &lt; Limit</c>
/// (<see cref="TryConsumeAsync"/>) — not a separate read-then-write — so concurrent requests from the
/// same user can't both observe "under quota" and both proceed, over-spending the limit.
/// </summary>
internal sealed class UserLlmQuotaService(
    AppDbContext db, IOptions<LlmQuotaOptions> options, TimeProvider timeProvider) : IUserLlmQuotaService
{
    public async Task<bool> TryConsumeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (periodStart, limit) = CurrentPeriod();

        await EnsureRowExistsAsync(userId, periodStart, cancellationToken);

        var updated = await db.UserLlmUsagePeriods
            .Where(p => p.UserId == userId && p.PeriodStart == periodStart && p.CallCount < limit)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CallCount, p => p.CallCount + 1), cancellationToken);

        return updated > 0;
    }

    public async Task<UserQuotaStatus> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (periodStart, limit) = CurrentPeriod();

        var used = await db.UserLlmUsagePeriods
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.PeriodStart == periodStart)
            .Select(p => (int?)p.CallCount)
            .FirstOrDefaultAsync(cancellationToken) ?? 0;

        return new UserQuotaStatus(
            used, limit, periodStart, QuotaPeriodCalculator.PeriodEnd(periodStart, options.Value.PeriodDays));
    }

    private (DateTimeOffset PeriodStart, int Limit) CurrentPeriod()
    {
        var periodStart = QuotaPeriodCalculator.CurrentPeriodStart(timeProvider.GetUtcNow(), options.Value.PeriodDays);
        return (periodStart, options.Value.MaxCallsPerPeriod);
    }

    private async Task EnsureRowExistsAsync(Guid userId, DateTimeOffset periodStart, CancellationToken cancellationToken)
    {
        var exists = await db.UserLlmUsagePeriods
            .AnyAsync(p => p.UserId == userId && p.PeriodStart == periodStart, cancellationToken);
        if (exists)
        {
            return;
        }

        db.UserLlmUsagePeriods.Add(new UserLlmUsagePeriod
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PeriodStart = periodStart,
            CallCount = 0,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request raced us to create the same (user, period) row — the unique index
            // means exactly one insert wins; the row exists either way, so just proceed.
            db.ChangeTracker.Clear();
        }
    }
}
