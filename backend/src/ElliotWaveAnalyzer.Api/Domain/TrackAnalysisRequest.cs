namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Request to add an analysis to the caller's track record. The frontend fills this from a
/// ranked count and its forward levels.
/// </summary>
public sealed record TrackAnalysisRequest(
    string Symbol,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score)
{
    /// <summary>Entry (pullback) zone of the primary — fires a zone-entry alert when price reaches it.</summary>
    public decimal? EntryLow { get; init; }

    /// <summary>Upper bound of the primary's entry zone.</summary>
    public decimal? EntryHigh { get; init; }

    /// <summary>Backup counts (up to two) the auto-switch promotes from if the primary is invalidated.</summary>
    public IReadOnlyList<ScenarioInput> Alternates { get; init; } = [];
}
