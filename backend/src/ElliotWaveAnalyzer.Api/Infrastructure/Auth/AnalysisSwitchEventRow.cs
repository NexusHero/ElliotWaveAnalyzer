namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// An append-only audit record on an <see cref="AnalysisSnapshot"/>: when the primary scenario's
/// invalidation broke and the tree auto-switched to a promoted alternate. Never overwritten.
/// </summary>
internal sealed class AnalysisSwitchEventRow
{
    public Guid Id { get; set; }

    public Guid AnalysisSnapshotId { get; set; }

    public DateTimeOffset At { get; set; }

    public string FromLabel { get; set; } = string.Empty;

    public string ToLabel { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
