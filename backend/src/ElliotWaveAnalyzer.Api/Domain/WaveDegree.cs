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
