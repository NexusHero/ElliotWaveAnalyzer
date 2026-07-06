using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Runs an intelligent alternate-hypothesis pass for a symbol (#186): detect the pivots, let the LLM
/// propose structures worth testing, then deterministically generate + rule-check each — validated
/// ones scored, rejected ones carrying the failing rule. The LLM proposes; the engine validates.
/// </summary>
public interface IAlternateHypothesisService
{
    /// <summary>Proposes and validates alternate structure hypotheses for <paramref name="symbol"/>.</summary>
    Task<AlternateHypothesesReport> AnalyzeAsync(
        string symbol,
        CandleInterval interval,
        CancellationToken cancellationToken = default);
}
