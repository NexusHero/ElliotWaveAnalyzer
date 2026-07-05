namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One count in a saved analysis's scenario tree: its direction, entry and target zones, hard
/// invalidation line, and a probability drawn from the measured track-record calibration (or an
/// explicit <see cref="ProbabilityBasis.InsufficientData"/> marker when the sample is too thin).
/// A <see cref="Retired"/> scenario is a former primary kept for the switch history.
/// </summary>
/// <param name="Role">Primary (in force) or Alternate (backup).</param>
/// <param name="Label">Stable human label, e.g. "Primary", "Alt 1".</param>
/// <param name="Structure">Pattern kind, e.g. "Impulse".</param>
/// <param name="Bullish">True when the count is directionally up.</param>
/// <param name="InvalidationPrice">The hard line that voids this scenario; null if undeterminable.</param>
/// <param name="InvalidationAbove">True when the invalidation line sits above price.</param>
/// <param name="EntryLow">Lower bound of the entry (pullback) zone; null if none.</param>
/// <param name="EntryHigh">Upper bound of the entry zone; null if none.</param>
/// <param name="TargetLow">Lower bound of the target zone; null if none.</param>
/// <param name="TargetHigh">Upper bound of the target zone; null if none.</param>
/// <param name="Confidence">The count's confidence label, used to pick its calibration bucket.</param>
/// <param name="Score">Deterministic guideline score in [0, 1]; null if unknown.</param>
/// <param name="Probability">Calibrated probability in [0, 1]; null when <see cref="ProbabilityBasis"/>
/// is <see cref="ProbabilityBasis.InsufficientData"/>.</param>
/// <param name="ProbabilityBasis">Whether <see cref="Probability"/> is measured or withheld.</param>
/// <param name="Retired">True for a former primary retained in the switch history.</param>
public sealed record Scenario(
    ScenarioRole Role,
    string Label,
    string Structure,
    bool Bullish,
    decimal? InvalidationPrice,
    bool InvalidationAbove,
    decimal? EntryLow,
    decimal? EntryHigh,
    decimal? TargetLow,
    decimal? TargetHigh,
    string Confidence,
    decimal? Score,
    decimal? Probability,
    ProbabilityBasis ProbabilityBasis,
    bool Retired);
