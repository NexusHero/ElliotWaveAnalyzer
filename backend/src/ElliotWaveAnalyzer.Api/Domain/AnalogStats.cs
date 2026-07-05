namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The aggregate, empirically-measured resolution of a set of historical analogs — the grounded
/// numbers the panel shows ("47 analogs, 68% reached target, median 12 days"). Every figure is
/// computed only from <em>concluded</em> analogs; pending setups never enter the denominator. When
/// too few analogs back the read, <see cref="Sufficient"/> is false and the rates are not to be
/// trusted (the UI shows an explicit "insufficient history" state rather than a single-sample rate).
/// </summary>
/// <param name="SampleCount">Number of concluded analogs behind these figures.</param>
/// <param name="TargetReached">How many reached their target.</param>
/// <param name="Invalidated">How many invalidated.</param>
/// <param name="HitRate">TargetReached / SampleCount in [0, 1], or null when SampleCount is 0.</param>
/// <param name="MedianResolutionDays">Median calendar days to resolution, or null when empty.</param>
/// <param name="Sufficient">True when SampleCount meets the minimum for the rates to be meaningful.</param>
public sealed record AnalogStats(
    int SampleCount,
    int TargetReached,
    int Invalidated,
    double? HitRate,
    double? MedianResolutionDays,
    bool Sufficient);
