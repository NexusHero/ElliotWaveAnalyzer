namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One alternate scenario supplied when saving an analysis. The primary is carried by the flat
/// fields of <see cref="TrackAnalysisRequest"/>; alternates are the backup counts the auto-switch
/// promotes from if the primary's invalidation breaks.
/// </summary>
/// <param name="Structure">Pattern kind, e.g. "Impulse".</param>
/// <param name="Bullish">True when the count is directionally up.</param>
/// <param name="InvalidationPrice">The hard line that voids this scenario; null if undeterminable.</param>
/// <param name="InvalidationAbove">True when the invalidation line sits above price.</param>
/// <param name="EntryLow">Lower bound of the entry (pullback) zone; null if none.</param>
/// <param name="EntryHigh">Upper bound of the entry zone; null if none.</param>
/// <param name="TargetLow">Lower bound of the target zone; null if none.</param>
/// <param name="TargetHigh">Upper bound of the target zone; null if none.</param>
/// <param name="Confidence">The count's confidence label (buckets the scenario's probability).</param>
/// <param name="Score">Deterministic guideline score in [0, 1]; null if unknown.</param>
public sealed record ScenarioInput(
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? EntryLow,
    decimal? EntryHigh,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score);
