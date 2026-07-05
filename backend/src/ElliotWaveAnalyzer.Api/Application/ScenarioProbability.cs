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
    /// The probability for a scenario whose confidence matches <paramref name="bucket"/>, optionally
    /// informed by a <paramref name="backtestPrior"/> (the hit-rate the harness measured for this
    /// confidence over history, REQ-026):
    /// <list type="bullet">
    /// <item>enough concluded analyses → the measured hit-rate, blended toward the prior when one is
    /// available (<see cref="ProbabilityBasis.Calibrated"/>);</item>
    /// <item>too few, but a prior exists → the prior (<see cref="ProbabilityBasis.Backtested"/>);</item>
    /// <item>neither → <see cref="ProbabilityBasis.InsufficientData"/>.</item>
    /// </list>
    /// </summary>
    public static Estimate From(
        CalibrationBucket? bucket, decimal? backtestPrior = null, int minimumSample = DefaultMinimumSample)
    {
        var measured = bucket is not null && bucket.Concluded >= minimumSample ? bucket.HitRate : null;
        if (measured is { } m)
        {
            var blended = backtestPrior is { } prior
                ? (MeasuredWeight * m) + ((1m - MeasuredWeight) * prior)
                : m;
            return new Estimate(blended, ProbabilityBasis.Calibrated);
        }

        if (backtestPrior is { } p)
        {
            return new Estimate(p, ProbabilityBasis.Backtested);
        }

        return new Estimate(null, ProbabilityBasis.InsufficientData);
    }

    /// <summary>
    /// Weight given to the user's own measured hit-rate when it is blended with the backtest prior.
    /// The measured rate leads (it is the user's real record); the prior only stabilizes it.
    /// </summary>
    private const decimal MeasuredWeight = 0.7m;
}
