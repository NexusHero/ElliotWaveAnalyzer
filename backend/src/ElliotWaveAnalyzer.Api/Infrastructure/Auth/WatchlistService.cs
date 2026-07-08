using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Watchlist store on the shared <see cref="AppDbContext"/> (#226). A brand-new user is seeded
/// with the four legacy quick symbols on first read, so the hardcoded strip's defaults survive as
/// the initial state of a now-editable list. Lives in Infrastructure because it touches EF and the
/// quote provider directly — consumers depend on <see cref="IWatchlistService"/>.
/// </summary>
internal sealed class WatchlistService(AppDbContext db, IQuoteProvider quotes, TimeProvider timeProvider)
    : IWatchlistService
{
    /// <summary>The pre-#226 hardcoded quick-symbol strip — seeded once per user (AC3).</summary>
    private static readonly string[] DefaultSymbols = ["SP500", "NASDAQ", "BTC", "ETH"];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WatchlistEntry>> ListAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.WatchlistEntries
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.SortOrder)
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            rows = await SeedDefaultsAsync(userId, cancellationToken);
        }

        var draftSymbols = await db.WorkspaceDrafts
            .Where(d => d.UserId == userId)
            .Select(d => d.Symbol)
            .Distinct()
            .ToListAsync(cancellationToken);
        var hasDraft = new HashSet<string>(draftSymbols, StringComparer.OrdinalIgnoreCase);

        var entries = new List<WatchlistEntry>(rows.Count);
        foreach (var row in rows)
        {
            var price = await quotes.GetLatestPriceAsync(row.Symbol, cancellationToken);
            entries.Add(new WatchlistEntry(row.Symbol, row.SortOrder, price, hasDraft.Contains(row.Symbol)));
        }

        return entries;
    }

    /// <inheritdoc/>
    public async Task AddAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var existing = await db.WatchlistEntries
            .Where(w => w.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existing.Any(w => w.Symbol == normalized))
        {
            return;
        }

        var nextSortOrder = existing.Count == 0 ? 0 : existing.Max(w => w.SortOrder) + 1;
        db.WatchlistEntries.Add(new WatchlistEntryRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = normalized,
            SortOrder = nextSortOrder,
            CreatedAt = timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(Guid userId, string symbol, CancellationToken cancellationToken = default)
    {
        var normalized = symbol.ToUpperInvariant();
        var row = await db.WatchlistEntries
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == normalized, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.WatchlistEntries.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<WatchlistEntryRow>> SeedDefaultsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rows = DefaultSymbols.Select((symbol, index) => new WatchlistEntryRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Symbol = symbol,
            SortOrder = index,
            CreatedAt = now,
        }).ToList();

        db.WatchlistEntries.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
        return rows;
    }
}
