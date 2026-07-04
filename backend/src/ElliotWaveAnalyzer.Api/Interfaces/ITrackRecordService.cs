using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// A user's Elliott Wave analysis track record: save an analysis, list saved analyses with
/// their outcome evaluated against live price action, and delete one. Every operation is
/// scoped to the calling user — no cross-user access.
/// </summary>
public interface ITrackRecordService
{
    /// <summary>Saves an analysis to <paramref name="userId"/>'s track record; returns its id.</summary>
    Task<Guid> SaveAsync(Guid userId, TrackAnalysisRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the user's saved analyses, most recent first, each with its outcome evaluated
    /// against the candles that formed since it was saved.
    /// </summary>
    Task<IReadOnlyList<TrackedAnalysis>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the analysis with <paramref name="id"/> if it belongs to <paramref name="userId"/>.
    /// Returns false when it does not exist or is owned by someone else.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
