using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Classifies each related instrument's own behavior as supporting or contradicting a count's thesis
/// (#188, AC3). Deterministic and pure — takes a bare direction flag and the readings' own numbers,
/// never the count's <see cref="WaveLevels"/> object itself, so it cannot interfere with the count's
/// geometry (AC4).
/// </summary>
public static class IntermarketDivergenceDetector
{
    /// <summary>
    /// Readings with an absolute correlation below this are too weak to read a corroboration or
    /// contradiction into and are excluded, not force-classified either way.
    /// </summary>
    public const double MinCorrelationMagnitude = 0.2;

    /// <summary>
    /// For a positively correlated instrument, its own move is expected to agree with
    /// <paramref name="thesisBullish"/>; for a negatively correlated one, expected to move opposite.
    /// A reading matching its expectation is <see cref="IntermarketSignalKind.Support"/>; one that
    /// doesn't is <see cref="IntermarketSignalKind.Contradiction"/>.
    /// </summary>
    public static IReadOnlyList<IntermarketSignal> Detect(bool thesisBullish, IReadOnlyList<IntermarketReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var signals = new List<IntermarketSignal>();
        foreach (var reading in readings)
        {
            if (Math.Abs(reading.Correlation) < MinCorrelationMagnitude)
            {
                continue;
            }

            var expectedBullish = reading.Correlation >= 0 ? thesisBullish : !thesisBullish;
            var readingBullish = reading.PercentChange >= 0;
            var kind = readingBullish == expectedBullish
                ? IntermarketSignalKind.Support
                : IntermarketSignalKind.Contradiction;

            signals.Add(new IntermarketSignal(reading.Symbol, reading.Correlation, reading.PercentChange, kind));
        }

        return signals;
    }
}
