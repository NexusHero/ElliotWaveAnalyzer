namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A saved in-progress workspace for one symbol+interval (#226): the placed annotations and the
/// chart settings the analyst had active, auto-restored when they switch back to this symbol.
/// Distinct from a track-record save — a draft carries no outcome tracking and is silently
/// overwritten by the next auto-save, never a deliberate "finished analysis" record.
/// </summary>
public sealed record WorkspaceDraft(
    string Symbol,
    string Interval,
    IReadOnlyList<WaveAnnotation> Annotations,
    WorkspaceDraftSettings Settings,
    DateTimeOffset UpdatedAt);
