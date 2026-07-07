namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One persona's full ranking of the deterministic candidates (mirrors a single provider's
/// <see cref="AutoWaveRanking"/> in the existing ensemble, tagged with which persona produced it).
/// The persona may only re-order and explain the candidates it was handed — it never introduces one.
/// </summary>
/// <param name="Persona">The persona's stable name, e.g. "Conservative", "Aggressive", "Contrarian".</param>
/// <param name="Ranking">The persona's own ranking, ids + prose only.</param>
public sealed record PersonaRanking(string Persona, AutoWaveRanking Ranking);
