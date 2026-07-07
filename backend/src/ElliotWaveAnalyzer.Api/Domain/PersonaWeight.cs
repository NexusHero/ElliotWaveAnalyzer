namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One persona's vote weight for the panel aggregation, derived from its own measured track record
/// (mirrors <see cref="ConfidenceCalibration"/>, scoped to one persona). Produced only by
/// <see cref="Application.PersonaWeightCalculator"/>.
/// </summary>
/// <param name="Persona">The persona's stable name.</param>
/// <param name="Weight">In [0, 1] — the persona's measured hit-rate, or the documented neutral prior.</param>
/// <param name="IsNeutralPrior">True when the persona has no concluded history yet (AC3).</param>
public sealed record PersonaWeight(string Persona, double Weight, bool IsNeutralPrior);
