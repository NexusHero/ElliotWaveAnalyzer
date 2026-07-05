using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Persists a user's most recently imported depot and reads it back. Each user has at most one
/// saved depot — a new import replaces the previous one.
/// </summary>
public interface IDepotStore
{
    /// <summary>Saves <paramref name="snapshot"/> as the user's current depot, replacing any prior one.</summary>
    Task SaveAsync(Guid userId, DepotSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's saved depot, or null if they have not imported one.</summary>
    Task<DepotSnapshot?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);
}
