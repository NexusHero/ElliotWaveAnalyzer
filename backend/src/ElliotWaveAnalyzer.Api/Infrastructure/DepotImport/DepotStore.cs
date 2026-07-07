using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Stores a user's imported depot snapshots in PostgreSQL via <see cref="AppDbContext"/> (#115).
/// Every import accumulates as a new, timestamped row — nothing is deleted on a new import, so the
/// full history survives (unbounded; no retention/pruning policy — a documented decision, see
/// ADR-051). <see cref="GetLatestAsync"/> remains the default "my current holdings" view.
/// </summary>
internal sealed class DepotStore(AppDbContext db) : IDepotStore
{
    public async Task SaveAsync(
        Guid userId, DepotSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        db.SavedDepots.Add(new SavedDepot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Source = snapshot.Source,
            ImportedAt = snapshot.ImportedAt,
            ExportedAt = snapshot.ExportedAt,
            Currency = snapshot.Currency,
            TotalValue = snapshot.Totals?.TotalValue,
            GainAbsolute = snapshot.Totals?.GainAbsolute,
            GainRelativePercent = snapshot.Totals?.GainRelativePercent,
            Positions = [.. snapshot.Positions.Select((p, ordinal) => new SavedDepotPosition
            {
                Id = Guid.NewGuid(),
                Ordinal = ordinal,
                Isin = p.Isin,
                Wkn = p.Wkn,
                Name = p.Name,
                Quantity = p.Quantity,
                CostPrice = p.CostPrice,
                CostValue = p.CostValue,
                MarketPrice = p.MarketPrice,
                MarketValue = p.MarketValue,
                GainAbsolute = p.GainAbsolute,
                GainRelativePercent = p.GainRelativePercent,
                Exchange = p.Exchange,
            })],
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<DepotSnapshot?> GetLatestAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var depot = await db.SavedDepots
            .AsNoTracking()
            .Include(d => d.Positions)
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.ImportedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return depot is null ? null : ToSnapshot(depot);
    }

    public async Task<IReadOnlyList<DepotHistoryEntry>> GetHistoryAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await db.SavedDepots
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.ImportedAt)
            .Select(d => new DepotHistoryEntry(
                d.Id, d.Source, d.ImportedAt, d.ExportedAt, d.Currency,
                d.TotalValue == null && d.GainAbsolute == null && d.GainRelativePercent == null
                    ? null
                    : new DepotTotals(d.TotalValue, d.GainAbsolute, d.GainRelativePercent)))
            .ToListAsync(cancellationToken);
    }

    public async Task<DepotSnapshot?> GetByIdAsync(
        Guid userId, Guid id, CancellationToken cancellationToken = default)
    {
        var depot = await db.SavedDepots
            .AsNoTracking()
            .Include(d => d.Positions)
            .FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, cancellationToken);

        return depot is null ? null : ToSnapshot(depot);
    }

    private static DepotSnapshot ToSnapshot(SavedDepot depot)
    {
        var positions = depot.Positions
            .OrderBy(p => p.Ordinal)
            .Select(p => new DepotPosition(
                p.Isin, p.Wkn, p.Name, p.Quantity, p.CostPrice, p.CostValue,
                p.MarketPrice, p.MarketValue, p.GainAbsolute, p.GainRelativePercent, p.Exchange))
            .ToList();

        var totals = depot.TotalValue is null && depot.GainAbsolute is null && depot.GainRelativePercent is null
            ? null
            : new DepotTotals(depot.TotalValue, depot.GainAbsolute, depot.GainRelativePercent);

        return new DepotSnapshot(
            depot.Source, depot.ImportedAt, depot.ExportedAt, depot.Currency, positions, totals);
    }
}
