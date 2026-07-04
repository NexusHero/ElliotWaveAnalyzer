namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Elliott Wave degree — the size class of a wave. A wave of one degree subdivides into
/// waves of the next smaller degree (Primary → Intermediate → Minor → Minute), which is what
/// multi-scale pivot detection and the nested wave parser use to relate counts across scales.
/// Only the mid-ladder degrees relevant to daily-candle analysis are modelled; the full
/// canonical ladder (Grand Supercycle … Subminuette) can be added later without breaking
/// consumers because values are serialized by name.
/// </summary>
public enum WaveDegree
{
    /// <summary>The smallest modelled degree (finest pivot scale).</summary>
    Minute,

    /// <summary>One step above Minute.</summary>
    Minor,

    /// <summary>One step above Minor.</summary>
    Intermediate,

    /// <summary>The default coarsest degree for daily-candle analysis.</summary>
    Primary,

    /// <summary>Above Primary; only used when more than four scales are requested.</summary>
    Cycle,
}

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
