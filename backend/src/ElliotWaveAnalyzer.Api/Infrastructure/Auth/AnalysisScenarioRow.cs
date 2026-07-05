using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Infrastructure.Auth;

/// <summary>
/// One persisted scenario of an <see cref="AnalysisSnapshot"/>'s tree: the primary count in force
/// plus its alternates. When the primary's invalidation breaks, the best alternate is promoted
/// (its <see cref="Role"/> becomes Primary) and the old primary is kept with <see cref="Retired"/>
/// set, so the tree carries its own history. Probability is not stored — it is computed on read
/// from the user's live calibration so it always reflects the latest measured hit-rate.
/// </summary>
internal sealed class AnalysisScenarioRow
{
    public Guid Id { get; set; }

    public Guid AnalysisSnapshotId { get; set; }

    public ScenarioRole Role { get; set; }

    /// <summary>Stable ordering within the tree (0 = primary, then alternates).</summary>
    public int OrderIndex { get; set; }

    public string Label { get; set; } = string.Empty;

    public string Structure { get; set; } = string.Empty;

    public bool Bullish { get; set; }

    public decimal? InvalidationPrice { get; set; }

    public bool InvalidationAbove { get; set; }

    public decimal? EntryLow { get; set; }

    public decimal? EntryHigh { get; set; }

    public decimal? TargetLow { get; set; }

    public decimal? TargetHigh { get; set; }

    public string Confidence { get; set; } = string.Empty;

    public decimal? Score { get; set; }

    /// <summary>True for a former primary retained for the switch history.</summary>
    public bool Retired { get; set; }
}
