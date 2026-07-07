using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Interfaces;

/// <summary>
/// Deterministically re-verifies an analyst-edited wave count against real candles (REQ-031): fetches
/// the symbol's history, snaps the edited pivots to candle extremes, and returns the full deterministic
/// read (hard rules, projections, score). No LLM — the geometry never depends on a model.
/// </summary>
public interface IWaveVerificationService
{
    /// <summary>
    /// Verifies <paramref name="annotations"/> for <paramref name="symbol"/> over the last
    /// <paramref name="lookbackDays"/> days of candles at <paramref name="interval"/> — the same
    /// series the chart displayed, so snapped pivots match what the analyst actually clicked.
    /// </summary>
    /// <exception cref="ArgumentException">When no provider supports the symbol.</exception>
    Task<WaveVerification> VerifyAsync(
        string symbol,
        IReadOnlyList<WaveAnnotation> annotations,
        int lookbackDays,
        CandleInterval interval = CandleInterval.OneDay,
        CancellationToken cancellationToken = default);
}
