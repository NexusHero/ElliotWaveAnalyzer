namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The kind of Elliott channel projected from a count. The <see cref="Base"/> channel is anchored
/// on the wave 0→2 line with a parallel through wave 1; the <see cref="Acceleration"/> channel is
/// anchored on wave 2→4 with a parallel through wave 3 and projects the wave-5 target band.
/// </summary>
public enum ChannelKind
{
    /// <summary>0→2 baseline, parallel through wave 1.</summary>
    Base,

    /// <summary>2→4 baseline, parallel through wave 3 — projects the wave-5 target.</summary>
    Acceleration,
}
