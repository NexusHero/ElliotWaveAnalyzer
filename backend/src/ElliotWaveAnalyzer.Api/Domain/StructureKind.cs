namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The Elliott Wave pattern family a structure belongs to. Motive structures (Impulse,
/// Diagonal) travel with the one-larger-degree trend; corrective structures (Zigzag, Flat,
/// Triangle) travel against it. Serialized by name so the wire format matches the
/// human-readable structure strings the API has always emitted (e.g. "Impulse").
/// </summary>
public enum StructureKind
{
    /// <summary>Five-wave motive structure obeying all three canonical rules.</summary>
    Impulse,

    /// <summary>Five-wave motive wedge; wave 4 may overlap wave 1, legs contract.</summary>
    Diagonal,

    /// <summary>Sharp A-B-C correction (5-3-5); B holds well short of A's origin.</summary>
    Zigzag,

    /// <summary>Sideways A-B-C correction (3-3-5); B retraces at least ~90% of A.</summary>
    Flat,

    /// <summary>Five-legged contracting consolidation (A-B-C-D-E, each a three).</summary>
    Triangle,
}
