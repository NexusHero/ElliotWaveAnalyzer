using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Runs every persona (#184) over the same deterministic candidate set and returns their
/// individual rankings plus their measured weights. Mirrors <see cref="IAutoWaveAnalyzer"/>'s
/// shape (candles + candidates in, LLM-authored ranking + token usage out) — personas only rank
/// and explain, never generate a candidate (ADR-009).
/// </summary>
public interface IPersonaAnalystPanel
{
    Task<PersonaPanelRankResult> RankAsync(
        Guid userId,
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default);
}
