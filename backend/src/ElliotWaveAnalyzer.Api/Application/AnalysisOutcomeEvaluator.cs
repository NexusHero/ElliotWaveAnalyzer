using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Pure evaluator for a tracked analysis: given the count's invalidation line and target zone
/// and the candles that formed after it was saved, decides whether the count was invalidated,
/// reached its target, or is still pending — whichever happened first in time.
///
/// WHY pure (static, no I/O): the outcome is a deterministic function of geometry and price,
/// so it belongs with the other pure Application checkers (<see cref="ElliottRuleChecker"/>,
/// <see cref="ProjectionService"/>) and is exhaustively unit-testable without a database or a
/// market-data provider.
///
/// Candle interpretation: a wick touch counts. The invalidation is crossed when a candle
/// trades through the line (High ≥ line when the line is above, Low ≤ line when below). The
/// target is reached when a candle trades into the zone in the direction of the count. When
/// both happen on the same candle the invalidation wins — it is the risk that matters.
/// </summary>
public static class AnalysisOutcomeEvaluator
{
    /// <summary>
    /// Evaluates the outcome. <paramref name="candlesAfter"/> must contain only candles that
    /// formed strictly after the analysis was saved, ascending by time (the caller filters).
    /// Returns <see cref="AnalysisOutcome.Pending"/> with null price/date when there are none.
    /// </summary>
    public static OutcomeEvaluation Evaluate(
        bool bullish,
        decimal? invalidationPrice,
        bool invalidationAbove,
        decimal? targetLow,
        decimal? targetHigh,
        IReadOnlyList<MarketCandle> candlesAfter)
    {
        ArgumentNullException.ThrowIfNull(candlesAfter);
        if (candlesAfter.Count == 0)
        {
            return new OutcomeEvaluation(AnalysisOutcome.Pending, null, null);
        }

        foreach (var candle in candlesAfter.OrderBy(c => c.OpenTime))
        {
            var invalidated = invalidationPrice is { } line
                && (invalidationAbove ? candle.High >= line : candle.Low <= line);
            if (invalidated)
            {
                return new OutcomeEvaluation(AnalysisOutcome.Invalidated, candle.Close, candle.OpenTime);
            }

            if (ReachedTarget(bullish, targetLow, targetHigh, candle))
            {
                return new OutcomeEvaluation(AnalysisOutcome.TargetReached, candle.Close, candle.OpenTime);
            }
        }

        // Nothing settled it: still open. Report the latest candle as the evaluation point.
        var last = candlesAfter[^1];
        return new OutcomeEvaluation(AnalysisOutcome.Pending, last.Close, last.OpenTime);
    }

    // A bullish count targets higher prices (zone above): reached when a high enters the zone.
    // A bearish count targets lower prices (zone below): reached when a low enters the zone.
    private static bool ReachedTarget(bool bullish, decimal? low, decimal? high, MarketCandle candle)
    {
        if (low is not { } lo || high is not { } hi)
        {
            return false;
        }

        return bullish ? candle.High >= lo : candle.Low <= hi;
    }
}
