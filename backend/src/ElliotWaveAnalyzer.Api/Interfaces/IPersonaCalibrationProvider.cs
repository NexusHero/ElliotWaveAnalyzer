using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Reads a user's real, persona-tagged track-record history (#184) — the "measured from real
/// outcomes" half of the panel's weighting <see cref="Application.PersonaWeightCalculator"/>
/// consumes. A persona with no tagged, concluded analyses yet simply has an empty outcome list,
/// which <see cref="Application.PersonaWeightCalculator"/> already turns into the documented
/// neutral prior (AC3) — there is no separate "no data" branch to implement here.
/// </summary>
public interface IPersonaCalibrationProvider
{
    /// <summary>
    /// For each of <paramref name="personaKeys"/>, the caller's own (confidence, outcome) history
    /// from analyses saved with that persona tag — outcomes evaluated live, same as the plain
    /// track record.
    /// </summary>
    Task<IReadOnlyList<(string Persona, IReadOnlyList<(string Confidence, AnalysisOutcome Outcome)> Outcomes)>> GetHistoryAsync(
        Guid userId, IReadOnlyList<string> personaKeys, CancellationToken cancellationToken = default);
}
