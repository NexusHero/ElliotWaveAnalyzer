namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// A deterministic risk read for a trade idea derived from a count's geometry: the stop (the count's
/// hard invalidation), how far it is from entry, the reward:risk to each target, and the position size
/// that risks exactly the chosen capital. Pure arithmetic on the user's own inputs — <b>not trading
/// advice</b>. Computed by <see cref="Application.RiskCalculator"/>; no LLM.
/// </summary>
/// <param name="HasValidStop">False when entry sits on the wrong side of the invalidation (or on it),
/// so there is no stop between entry and the invalidation. When false, sizing and R:R are omitted.</param>
/// <param name="NoStopReason">Human explanation when <see cref="HasValidStop"/> is false; null otherwise.</param>
/// <param name="Bullish">True for a long (stop below entry), false for a short (stop above entry).</param>
/// <param name="Entry">The entry price the assessment was computed for.</param>
/// <param name="StopPrice">The stop = the count's invalidation line.</param>
/// <param name="StopDistanceAbs">Absolute distance |entry − stop|; 0 only when there is no valid stop.</param>
/// <param name="StopDistancePct">Stop distance as a fraction of entry (0.10 = 10%).</param>
/// <param name="RiskCapital">The capital the position is sized to risk (the resolved account-risk amount).</param>
/// <param name="SuggestedSize">Units to buy/sell so a stop-out loses exactly <see cref="RiskCapital"/>;
/// null when there is no valid stop or the risk capital is not positive.</param>
/// <param name="Notional">Suggested size × entry — the capital deployed; null when size is null.</param>
/// <param name="Targets">Per-target reward:risk, ordered by price ascending.</param>
public sealed record RiskAssessment(
    bool HasValidStop,
    string? NoStopReason,
    bool Bullish,
    decimal Entry,
    decimal StopPrice,
    decimal StopDistanceAbs,
    decimal StopDistancePct,
    decimal RiskCapital,
    decimal? SuggestedSize,
    decimal? Notional,
    IReadOnlyList<TargetRisk> Targets);
