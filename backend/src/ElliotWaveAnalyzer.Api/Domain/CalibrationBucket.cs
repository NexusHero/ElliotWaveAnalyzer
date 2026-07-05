namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Calibration of one confidence level against recorded outcomes: of the saved analyses the LLM
/// rated at this confidence, how many have concluded and how many of those reached their target.
/// </summary>
/// <param name="Confidence">The confidence label, normalized ("high" / "medium" / "low" / other).</param>
/// <param name="Total">All saved analyses at this confidence.</param>
/// <param name="Concluded">Those that have concluded (target reached or invalidated; pending excluded).</param>
/// <param name="TargetReached">Concluded analyses that reached their target.</param>
/// <param name="Invalidated">Concluded analyses that were invalidated.</param>
/// <param name="HitRate">TargetReached ÷ Concluded, in [0, 1]; null when none have concluded.</param>
public sealed record CalibrationBucket(
    string Confidence,
    int Total,
    int Concluded,
    int TargetReached,
    int Invalidated,
    decimal? HitRate);
