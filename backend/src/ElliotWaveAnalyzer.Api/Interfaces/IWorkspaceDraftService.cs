using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// A user's per-symbol+interval workspace drafts (#226): the in-progress annotations and chart
/// settings auto-saved as the analyst works, auto-restored when they switch back. Every operation
/// is scoped to the calling user — no cross-user access.
/// </summary>
public interface IWorkspaceDraftService
{
    /// <summary>The draft for <paramref name="symbol"/>+<paramref name="interval"/>, or null when none exists.</summary>
    Task<WorkspaceDraft?> GetAsync(
        Guid userId, string symbol, string interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts the draft for <paramref name="symbol"/>+<paramref name="interval"/>. When the user's
    /// draft count exceeds the cap, the least-recently-updated draft(s) are evicted.
    /// </summary>
    Task SaveAsync(
        Guid userId,
        string symbol,
        string interval,
        SaveWorkspaceDraftRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the draft for <paramref name="symbol"/>+<paramref name="interval"/>; false when none existed.</summary>
    Task<bool> DeleteAsync(
        Guid userId, string symbol, string interval, CancellationToken cancellationToken = default);
}
