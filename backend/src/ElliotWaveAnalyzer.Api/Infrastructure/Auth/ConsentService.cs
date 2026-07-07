using ElliotWaveAnalyzer.Api.Interfaces;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary><see cref="IConsentService"/> backed by <see cref="AppDbContext"/>.</summary>
internal sealed class ConsentService(AppDbContext db, TimeProvider timeProvider) : IConsentService
{
    /// <inheritdoc/>
    public async Task RecordAsync(
        string visitorId,
        bool analytics,
        bool marketing,
        string policyVersion,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        db.ConsentRecords.Add(new ConsentRecord
        {
            Id = Guid.NewGuid(),
            VisitorId = visitorId,
            UserId = userId,
            Analytics = analytics,
            Marketing = marketing,
            PolicyVersion = policyVersion,
            RecordedAt = timeProvider.GetUtcNow(),
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
