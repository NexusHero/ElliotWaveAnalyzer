using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The deterministic half of #186: a proposed structure is generated over the pivots and rule-checked.
/// A rule-violating proposal is dropped with its failing rule and never marked valid (AC2); a valid one
/// is accepted and scored (AC3); the verdict is reproducible (AC4).
/// </summary>
[TestFixture]
public sealed class HypothesisValidatorTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Pivots (origin + waves), alternating low/high, as a bullish impulse unless noted.
    private static IReadOnlyList<SwingPivot> Pivots(params decimal[] prices) =>
        prices.Select((p, i) => new SwingPivot(Start.AddDays(i * 5), p, IsHigh: i % 2 == 1)).ToList();

    [Test]
    public void Validate_ValidImpulse_IsAcceptedAndScored()
    {
        // 100→130 (w1) →115 (w2) →175 (w3) →150 (w4) →200 (w5): all three impulse rules hold.
        var result = HypothesisValidator.Validate(
            StructureKind.Impulse, "clean five up", Pivots(100, 130, 115, 175, 150, 200));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.FailingRule, Is.Null);
            Assert.That(result.Structure, Is.EqualTo("Impulse"));
            Assert.That(result.Score, Is.Not.Null);
        });
    }

    [Test]
    public void Validate_RuleViolatingImpulse_IsRejectedWithTheFailingRule_NeverValid()
    {
        // Wave 4 (drops to 125) overlaps Wave 1's top (130) → Rule 3 fails.
        var result = HypothesisValidator.Validate(
            StructureKind.Impulse, "maybe an impulse", Pivots(100, 130, 115, 175, 125, 200));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Score, Is.Null);
            Assert.That(result.FailingRule, Does.Contain("Wave 4"));
        });
    }

    [Test]
    public void Validate_TooFewPivots_IsRejectedWithACount()
    {
        var result = HypothesisValidator.Validate(
            StructureKind.Impulse, "impulse", Pivots(100, 130, 115));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailingRule, Does.Contain("Needs 6 pivots"));
        });
    }

    [Test]
    public void Validate_IsDeterministic()
    {
        var pivots = Pivots(100, 130, 115, 175, 150, 200);
        var first = HypothesisValidator.Validate(StructureKind.Impulse, "r", pivots);
        var second = HypothesisValidator.Validate(StructureKind.Impulse, "r", pivots);
        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Validate_CorrectiveProposal_RunsTheZigzagChecker()
    {
        // A bearish A-B-C: 200 →150 (A) →175 (B, holds within A's origin) →120 (C).
        var result = HypothesisValidator.Validate(
            StructureKind.Zigzag, "sharp abc down", Pivots(200, 150, 175, 120));

        // Whatever the verdict, it is produced by the zigzag path and never throws.
        Assert.That(result.Structure, Is.EqualTo("Zigzag"));
    }
}
