using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Ranks pre-validated <see cref="WaveCandidate"/> counts via an LLM and returns the most
/// likely interpretation plus a market read. The geometry comes from the deterministic
/// generator; the LLM only judges and explains — it returns candidate ids and prose, never
/// prices, so the structure shown to the user stays grounded.
/// </summary>
public interface IAutoWaveAnalyzer
{
    /// <summary>Provider identifier used for reporting ("Gemini", "Claude", "OpenAI").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Ranks the candidates against the market context and returns the ranking with the token
    /// usage of the call. Callers should not invoke this with an empty candidate set.
    /// </summary>
    Task<AutoWaveAnalysis> RankAsync(
        string symbol,
        IReadOnlyList<MarketCandle> candles,
        IReadOnlyList<WaveCandidate> candidates,
        CancellationToken cancellationToken = default);
}
