namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The broad Elliott family a wave's substructure is expected to take. Motive waves (impulses,
/// diagonals) travel with the one-larger-degree trend; corrective waves (zigzags, flats,
/// triangles) travel against it. The top-down analyzer uses this as a <em>soft</em> constraint:
/// a finer count whose class disagrees with the parent wave is penalized, not rejected (only a
/// direction contradiction is a hard reject).
/// </summary>
public enum StructureClass
{
    /// <summary>Impulsive substructure expected (impulse or diagonal).</summary>
    Motive,

    /// <summary>Corrective substructure expected (zigzag, flat or triangle).</summary>
    Corrective,
}
