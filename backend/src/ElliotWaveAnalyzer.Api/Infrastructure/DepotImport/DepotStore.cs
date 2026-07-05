using ElliotWaveAnalyzer.Api.Domain.Depot;
using ElliotWaveAnalyzer.Api.Infrastructure.Auth;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.DepotImport;

/// <summary>
/// Stores a user's imported depot in PostgreSQL via <see cref="AppDbContext"/>. Upserts: a new
/// import replaces the user's previous saved depot (holdings cascade-delete with it).
/// </summary>
internal sealed class DepotStore(AppDbContext db) : IDepotStore
{
    public async Task SaveAsync(
        Guid userId, DepotSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var existing = await db.SavedDepots
            .Where(d => d.UserId == userId)
            .ToListAsync(cancellationToken);
        if (existing.Count > 0)
        {
            db.SavedDepots.RemoveRange(existing); // cascade removes its positions
        }

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
            Positions = [.. snapshot.Positions.Select(p => new SavedDepotPosition
            {
                Id = Guid.NewGuid(),
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
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
        if (depot is null)
        {
            return null;
        }

        var positions = depot.Positions
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
