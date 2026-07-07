namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A single recorded miss — a concluded tracked analysis that invalidated rather than reaching its
/// target — produced only by <see cref="Application.MissSetCalculator"/> (#189, AC1). The unit of
/// evidence an LLM-proposed <see cref="Lesson"/> must cite by <see cref="Id"/> to be taken seriously.
/// </summary>
/// <param name="Id">The underlying tracked analysis's stable id.</param>
/// <param name="Symbol">Instrument symbol.</param>
/// <param name="Structure">Pattern kind, e.g. "Impulse" or "Zigzag".</param>
/// <param name="Confidence">Normalized confidence label at save time ("high"/"medium"/"low"/"unknown").</param>
/// <param name="CreatedAt">When the analysis was saved.</param>
public sealed record MissCase(Guid Id, string Symbol, string Structure, string Confidence, DateTimeOffset CreatedAt);
