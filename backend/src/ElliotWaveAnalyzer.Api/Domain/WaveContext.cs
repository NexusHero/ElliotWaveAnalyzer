namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The constraint a higher-timeframe count imposes on the next finer timeframe: the wave that is
/// currently unfolding on the coarse chart, expressed as what the fine chart must therefore be
/// counting. Derived deterministically from the coarse count's forward levels
/// (<see cref="WaveLevels"/>) — never from an LLM. A finer count is <em>rejected</em> if it
/// travels against <see cref="ExpectedDirection"/>, and <em>penalized</em> if its class or price
/// range disagrees with <see cref="ExpectedClass"/> / the price window.
/// </summary>
/// <param name="ParentWaveLabel">The unfolding coarse wave, e.g. "Wave 2" or "Correction (ABC)".</param>
/// <param name="ExpectedDirection">Net direction the finer count should travel.</param>
/// <param name="ExpectedClass">Substructure family the finer count should take (soft constraint).</param>
/// <param name="WindowLow">Lower price bound the finer count is expected to stay within.</param>
/// <param name="WindowHigh">Upper price bound the finer count is expected to stay within.</param>
/// <param name="ParentDegree">Elliott degree of the parent wave; the finer count sits one below.</param>
public sealed record WaveContext(
    string ParentWaveLabel,
    TrendDirection ExpectedDirection,
    StructureClass ExpectedClass,
    decimal WindowLow,
    decimal WindowHigh,
    WaveDegree ParentDegree);
