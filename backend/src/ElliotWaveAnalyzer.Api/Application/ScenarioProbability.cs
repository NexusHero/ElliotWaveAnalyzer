using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Maps a confidence <see cref="CalibrationBucket"/> (measured from the user's own concluded
/// analyses) to a scenario probability. A number is only published when the bucket has at least
/// <see cref="DefaultMinimumSample"/> concluded analyses; below that the estimate is withheld as
/// <see cref="ProbabilityBasis.InsufficientData"/> rather than invented. Pure and deterministic.
/// </summary>
public static class ScenarioProbability
{
    /// <summary>Fewest concluded analyses in a bucket before its hit-rate is publishable.</summary>
    public const int DefaultMinimumSample = 10;

    /// <summary>A probability plus where it came from.</summary>
    public readonly record struct Estimate(decimal? Probability, ProbabilityBasis Basis);

    /// <summary>
    /// The probability for a scenario whose confidence matches <paramref name="bucket"/>. Returns
    /// the bucket's measured hit-rate when it has enough concluded analyses, otherwise
    /// <see cref="ProbabilityBasis.InsufficientData"/>.
    /// </summary>
    public static Estimate From(CalibrationBucket? bucket, int minimumSample = DefaultMinimumSample)
    {
        if (bucket is null || bucket.Concluded < minimumSample || bucket.HitRate is null)
        {
            return new Estimate(null, ProbabilityBasis.InsufficientData);
        }

        return new Estimate(bucket.HitRate, ProbabilityBasis.Calibrated);
    }
}
