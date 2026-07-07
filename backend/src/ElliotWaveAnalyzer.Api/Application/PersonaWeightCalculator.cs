using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Derives each persona's panel vote weight from its own recorded (confidence, outcome) history —
/// reusing <see cref="CalibrationCalculator"/>'s proven, pure hit-rate math (so "pending excluded"
/// and "no history" both fall out of the same calculation the existing calibration endpoint already
/// trusts). A persona with no <em>concluded</em> history yet gets the documented
/// <see cref="NeutralPrior"/> rather than a fabricated weight (AC3) — a pending pick never moves a
/// persona's weight, because <see cref="ConfidenceCalibration.OverallHitRate"/> is only computed from
/// concluded outcomes (AC6). Pure and static, so it is exhaustively unit-testable.
/// </summary>
public static class PersonaWeightCalculator
{
    /// <summary>
    /// The documented weight for a persona with no concluded history: neither trusted above nor
    /// discounted below an untested vote — every persona starts on equal footing.
    /// </summary>
    public const double NeutralPrior = 0.5;

    /// <summary>
    /// Computes one <see cref="PersonaWeight"/> per entry in <paramref name="history"/>, in the same
    /// order the personas are given.
    /// </summary>
    public static IReadOnlyList<PersonaWeight> Calculate(
        IReadOnlyList<(string Persona, IReadOnlyList<(string Confidence, AnalysisOutcome Outcome)> Outcomes)> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return history
            .Select(h =>
            {
                var calibration = CalibrationCalculator.Calculate(h.Outcomes);
                return calibration.OverallHitRate is { } hitRate
                    ? new PersonaWeight(h.Persona, (double)hitRate, IsNeutralPrior: false)
                    : new PersonaWeight(h.Persona, NeutralPrior, IsNeutralPrior: true);
            })
            .ToList();
    }
}
