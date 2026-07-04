using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for the pure <see cref="AnalysisOutcomeEvaluator"/>: invalidation-first,
/// target-first, still-pending, too-recent, and the same-candle tie-break — bullish and
/// bearish. Candles are built with explicit highs/lows since the evaluator is wick-aware.
/// </summary>
[TestFixture]
public sealed class AnalysisOutcomeEvaluatorTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds candles from (high, low) pairs, one day apart; open/close mid-range.</summary>
    private static IReadOnlyList<MarketCandle> Candles(params (decimal High, decimal Low)[] bars)
        => [.. bars.Select((b, i) =>
        {
            var mid = (b.High + b.Low) / 2m;
            return new MarketCandle(Start.AddDays(i), mid, b.High, b.Low, mid, 0m);
        })];

    [Test]
    public void NoCandlesYet_IsPendingWithNoEvaluationPoint()
    {
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m, candlesAfter: []);

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Pending));
            Assert.That(result.Price, Is.Null);
            Assert.That(result.At, Is.Null);
        });
    }

    [Test]
    public void Bullish_PriceHoldsBelowTargetAndAboveInvalidation_IsPendingAtLatestCandle()
    {
        // Bullish count: invalidation at 100 (below), target 150–160. Price drifts 120→130.
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m,
            candlesAfter: Candles((122m, 118m), (131m, 125m)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Pending));
            Assert.That(result.Price, Is.EqualTo(128m)); // mid of the last candle
            Assert.That(result.At!.Value.UtcDateTime, Is.EqualTo(Start.AddDays(1)));
        });
    }

    [Test]
    public void Bullish_LowCrossesInvalidation_IsInvalidatedOnThatCandle()
    {
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m,
            candlesAfter: Candles((122m, 118m), (119m, 99m), (140m, 130m)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Invalidated));
            Assert.That(result.At!.Value.UtcDateTime, Is.EqualTo(Start.AddDays(1))); // the candle with low 99
        });
    }

    [Test]
    public void Bullish_HighEntersTargetZone_IsTargetReached()
    {
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m,
            candlesAfter: Candles((130m, 125m), (152m, 145m)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.TargetReached));
            Assert.That(result.At!.Value.UtcDateTime, Is.EqualTo(Start.AddDays(1)));
        });
    }

    [Test]
    public void FirstEventWins_InvalidationBeforeTarget_IsInvalidated()
    {
        // Invalidation is breached on candle 0; target only on candle 1. Earliest wins.
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m,
            candlesAfter: Candles((120m, 95m), (155m, 150m)));

        Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Invalidated));
    }

    [Test]
    public void SameCandleHitsBoth_InvalidationWins()
    {
        // One candle spans both the invalidation (low 95) and the target (high 155).
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: 150m, targetHigh: 160m,
            candlesAfter: Candles((155m, 95m)));

        Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Invalidated),
            "the risk that matters: a candle touching both resolves to invalidation");
    }

    [Test]
    public void Bearish_HighCrossesInvalidationAbove_IsInvalidated()
    {
        // Bearish count: invalidation sits ABOVE at 200; a move up through it voids the count.
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: false, invalidationPrice: 200m, invalidationAbove: true,
            targetLow: 140m, targetHigh: 150m,
            candlesAfter: Candles((180m, 175m), (205m, 195m)));

        Assert.Multiple(() =>
        {
            Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Invalidated));
            Assert.That(result.At!.Value.UtcDateTime, Is.EqualTo(Start.AddDays(1)));
        });
    }

    [Test]
    public void Bearish_LowEntersTargetZoneBelow_IsTargetReached()
    {
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: false, invalidationPrice: 200m, invalidationAbove: true,
            targetLow: 140m, targetHigh: 150m,
            candlesAfter: Candles((180m, 175m), (155m, 148m)));

        Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.TargetReached));
    }

    [Test]
    public void NoInvalidationOrTarget_NeverConcludes_StaysPending()
    {
        var result = AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: null, invalidationAbove: false,
            targetLow: null, targetHigh: null,
            candlesAfter: Candles((120m, 110m), (130m, 125m)));

        Assert.That(result.Outcome, Is.EqualTo(AnalysisOutcome.Pending));
    }

    [Test]
    public void NullCandles_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AnalysisOutcomeEvaluator.Evaluate(
            bullish: true, invalidationPrice: 100m, invalidationAbove: false,
            targetLow: null, targetHigh: null, candlesAfter: null!));
    }
}
