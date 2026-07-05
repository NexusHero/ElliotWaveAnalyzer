using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Turns a count's geometry (entry, the hard invalidation as the stop, target zones) plus a chosen
/// account-risk amount into a risk read: stop distance, reward:risk per target, and the position size
/// that risks exactly the chosen capital. Direction-aware and fully guarded — an entry on the wrong
/// side of the invalidation yields an explicit "no valid stop" result, never a negative or infinite
/// size. Pure arithmetic, deterministic, no LLM. This is <b>not trading advice</b>; it is math on the
/// user's own inputs.
/// </summary>
public static class RiskCalculator
{
    /// <summary>
    /// Assess the risk for a trade idea.
    /// </summary>
    /// <param name="entry">Entry price (must be positive).</param>
    /// <param name="invalidation">The count's hard invalidation line — used as the stop.</param>
    /// <param name="targets">Target prices; reported ascending with R:R each.</param>
    /// <param name="bullish">True for a long (stop below entry), false for a short (stop above entry).</param>
    /// <param name="riskCapital">The absolute capital to risk on a stop-out (already resolved from a
    /// percentage of equity or an absolute amount by the caller). Non-positive → sizing is omitted.</param>
    public static RiskAssessment Assess(
        decimal entry,
        decimal invalidation,
        IReadOnlyList<decimal> targets,
        bool bullish,
        decimal riskCapital)
    {
        targets ??= [];

        // The stop must sit on the losing side of entry: below for a long, above for a short. Anything
        // else (including the stop exactly at entry) leaves no room between entry and the stop.
        var stopIsValid = bullish ? invalidation < entry : invalidation > entry;
        if (entry <= 0 || !stopIsValid)
        {
            var reason = entry <= 0
                ? "Entry must be a positive price."
                : bullish
                    ? $"For a long, the invalidation ({invalidation}) must be below entry ({entry})."
                    : $"For a short, the invalidation ({invalidation}) must be above entry ({entry}).";

            return new RiskAssessment(
                HasValidStop: false,
                NoStopReason: reason,
                Bullish: bullish,
                Entry: entry,
                StopPrice: invalidation,
                StopDistanceAbs: 0m,
                StopDistancePct: 0m,
                RiskCapital: riskCapital,
                SuggestedSize: null,
                Notional: null,
                Targets: []);
        }

        var stopDistance = Math.Abs(entry - invalidation);
        var stopPct = stopDistance / entry;

        // Sizing needs positive risk capital; without it we still report the stop and R:R but no size,
        // so a zero/negative account-risk input can never divide by zero or suggest a nonsense size.
        decimal? size = riskCapital > 0 ? riskCapital / stopDistance : null;
        decimal? notional = size * entry;

        var targetRisks = targets
            .OrderBy(price => price)
            .Select(price =>
            {
                var reward = bullish ? price - entry : entry - price;
                return new TargetRisk(price, reward, reward / stopDistance);
            })
            .ToList();

        return new RiskAssessment(
            HasValidStop: true,
            NoStopReason: null,
            Bullish: bullish,
            Entry: entry,
            StopPrice: invalidation,
            StopDistanceAbs: stopDistance,
            StopDistancePct: stopPct,
            RiskCapital: riskCapital,
            SuggestedSize: size,
            Notional: notional,
            Targets: targetRisks);
    }
}
