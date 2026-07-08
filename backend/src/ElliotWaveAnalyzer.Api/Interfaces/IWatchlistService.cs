using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// A user's watchlist (#226): the symbols they rotate through, replacing the old hardcoded quick
/// list. Every operation is scoped to the calling user — no cross-user access.
/// </summary>
public interface IWatchlistService
{
    /// <summary>
    /// Lists the user's watchlist, in display order. A user with no entries yet is seeded with the
    /// four legacy quick symbols (SP500, NASDAQ, BTC, ETH) on first read.
    /// </summary>
    Task<IReadOnlyList<WatchlistEntry>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Adds <paramref name="symbol"/> to the end of the user's watchlist. A no-op if already present.</summary>
    Task AddAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);

    /// <summary>Removes <paramref name="symbol"/> from the user's watchlist; false when it wasn't there.</summary>
    Task<bool> RemoveAsync(Guid userId, string symbol, CancellationToken cancellationToken = default);
}
