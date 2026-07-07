using ElliotWaveAnalyzer.Api.Domain.Depot;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Persists a user's imported depot snapshots and reads them back. Every import accumulates as a
/// timestamped snapshot (#115) — nothing is deleted on a new import; <see cref="GetLatestAsync"/> is
/// still the default "my current holdings" view.
/// </summary>
public interface IDepotStore
{
    /// <summary>Saves <paramref name="snapshot"/> as a new depot snapshot for the user.</summary>
    Task SaveAsync(Guid userId, DepotSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's most recently imported depot, or null if they have none.</summary>
    Task<DepotSnapshot?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the user's import history, newest first (headline metadata only).</summary>
    Task<IReadOnlyList<DepotHistoryEntry>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns one of the user's own saved snapshots by id, or null if it doesn't exist or isn't theirs.</summary>
    Task<DepotSnapshot?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
