using System.Text.Json;
using ElliotWaveAnalyzer.Api.Domain;
using ElliotWaveAnalyzer.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// Workspace-draft store on the shared <see cref="AppDbContext"/> (#226). Annotations and settings
/// are opaque, frontend-owned JSON — this service never interprets them, only persists and returns
/// them verbatim. Lives in Infrastructure because it touches EF directly — consumers depend on
/// <see cref="IWorkspaceDraftService"/>.
/// </summary>
internal sealed class WorkspaceDraftService(AppDbContext db, TimeProvider timeProvider) : IWorkspaceDraftService
{
    /// <summary>Per-user draft cap (#226 wish); the least-recently-updated draft is evicted past this.</summary>
    private const int MaxDraftsPerUser = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public async Task<WorkspaceDraft?> GetAsync(
        Guid userId, string symbol, string interval, CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(userId, symbol, interval, cancellationToken);
        return row is null ? null : ToDto(row);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(
        Guid userId,
        string symbol,
        string interval,
        SaveWorkspaceDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedSymbol = symbol.ToUpperInvariant();
        var row = await FindAsync(userId, normalizedSymbol, interval, cancellationToken);
        var now = timeProvider.GetUtcNow();

        if (row is null)
        {
            row = new WorkspaceDraftRow
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Symbol = normalizedSymbol,
                Interval = interval,
            };
            db.WorkspaceDrafts.Add(row);
        }

        row.AnnotationsJson = JsonSerializer.Serialize(request.Annotations, Json);
        row.SettingsJson = JsonSerializer.Serialize(request.Settings, Json);
        row.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        await EvictOldestPastCapAsync(userId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(
        Guid userId, string symbol, string interval, CancellationToken cancellationToken = default)
    {
        var row = await FindAsync(userId, symbol, interval, cancellationToken);
        if (row is null)
        {
            return false;
        }

        db.WorkspaceDrafts.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<WorkspaceDraftRow?> FindAsync(
        Guid userId, string symbol, string interval, CancellationToken cancellationToken)
    {
        var normalizedSymbol = symbol.ToUpperInvariant();
        return db.WorkspaceDrafts.FirstOrDefaultAsync(
            d => d.UserId == userId && d.Symbol == normalizedSymbol && d.Interval == interval,
            cancellationToken);
    }

    /// <summary>LRU eviction (#226 wish): keeps at most <see cref="MaxDraftsPerUser"/> drafts per user.</summary>
    private async Task EvictOldestPastCapAsync(Guid userId, CancellationToken cancellationToken)
    {
        var excess = await db.WorkspaceDrafts
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UpdatedAt)
            .Skip(MaxDraftsPerUser)
            .ToListAsync(cancellationToken);

        if (excess.Count == 0)
        {
            return;
        }

        db.WorkspaceDrafts.RemoveRange(excess);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static WorkspaceDraft ToDto(WorkspaceDraftRow row) => new(
        row.Symbol,
        row.Interval,
        JsonSerializer.Deserialize<IReadOnlyList<WaveAnnotation>>(row.AnnotationsJson, Json) ?? [],
        JsonSerializer.Deserialize<WorkspaceDraftSettings>(row.SettingsJson, Json)
            ?? new WorkspaceDraftSettings("impulse", true, false, false, false, false, null),
        row.UpdatedAt);
}
