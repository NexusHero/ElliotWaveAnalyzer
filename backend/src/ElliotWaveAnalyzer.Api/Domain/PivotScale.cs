namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The swing pivots detected at one ZigZag reversal threshold, tagged with the Elliott degree
/// that scale represents. Produced by <see cref="Application.SwingPivotDetector.DetectMultiScale"/>;
/// scales are ordered finest → coarsest and every coarser scale's pivots are a subset of the
/// next finer scale's (guaranteed by construction — coarse scales are derived by compressing
/// the finer pivot sequence, not by re-scanning candles).
/// </summary>
/// <param name="Degree">The Elliott degree this scale maps to.</param>
/// <param name="ThresholdPercent">The reversal threshold (in percent) that produced this scale.</param>
/// <param name="Pivots">Alternating high/low pivots, chronological.</param>
public sealed record PivotScale(
    WaveDegree Degree,
    decimal ThresholdPercent,
    IReadOnlyList<SwingPivot> Pivots);
