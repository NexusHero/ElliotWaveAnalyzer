using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Derives the <see cref="WaveContext"/> a coarse count imposes on the next finer timeframe:
/// the wave currently unfolding on the coarse chart, expressed as the direction, class and price
/// window the finer chart must therefore be counting within.
///
/// The unfolding wave and its price bounds come straight from the coarse count's deterministic
/// forward levels (<see cref="ProjectionService"/> → <see cref="WaveLevels"/>): a pullback wave
/// (2/4/B, triangle legs) has a support zone and its finer structure is corrective; a thrust wave
/// (3/5/C) has a target zone and is motive; a completing ABC correction is corrective by name.
/// Direction is simply "toward that destination from the last coarse pivot", so it is correct for
/// bullish and bearish counts alike. Pure and deterministic — no LLM.
/// </summary>
public static class WaveContextDeriver
{
    /// <summary>
    /// Derives the constraint for the timeframe below <paramref name="coarse"/>, or null when the
    /// count carries no forward levels to derive one from (e.g. too few pivots).
    /// </summary>
    public static WaveContext? Derive(WaveCandidate coarse)
    {
        ArgumentNullException.ThrowIfNull(coarse);

        var levels = coarse.Levels;
        if (levels is null || coarse.Waves.Count == 0)
        {
            return null;
        }

        var last = coarse.Waves[^1].Price;

        // A pullback wave hands the finer chart a corrective move toward its support zone; a thrust
        // wave hands it a motive move toward its (first) target zone. A completing ABC is corrective
        // regardless of which zone carries it.
        var isAbcCorrection = levels.UnfoldingWave.Contains("Correction", StringComparison.OrdinalIgnoreCase);
        decimal destination;
        StructureClass expectedClass;

        if (levels.SupportZone is { } support)
        {
            destination = Mid(support.Low, support.High);
            expectedClass = StructureClass.Corrective;
        }
        else if (levels.TargetZones.Count > 0)
        {
            var target = levels.TargetZones[0];
            destination = Mid(target.Low, target.High);
            expectedClass = isAbcCorrection ? StructureClass.Corrective : StructureClass.Motive;
        }
        else
        {
            return null;
        }

        var direction = destination >= last ? TrendDirection.Up : TrendDirection.Down;

        // The finer count is expected to stay between where the wave starts (the last coarse pivot),
        // where it is heading (the zone), and the line that would invalidate it (if any).
        var bounds = new List<decimal> { last, destination };
        if (levels.Invalidation is { } invalidation)
        {
            bounds.Add(invalidation.Price);
        }

        var parentDegree = coarse.Tree?.Degree ?? WaveDegree.Primary;

        return new WaveContext(
            levels.UnfoldingWave,
            direction,
            expectedClass,
            bounds.Min(),
            bounds.Max(),
            parentDegree);
    }

    private static decimal Mid(decimal a, decimal b) => (a + b) / 2m;
}
