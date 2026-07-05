using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The analyst-in-the-loop re-verification (REQ-031): an edited count is snapped to real candles and
/// gets the full deterministic read — hard rules, projections, score. A clean impulse validates and
/// scores; a wave-2 violation invalidates by name; a pivot off any candle is rejected, not trusted.
/// </summary>
[TestFixture]
public sealed class WaveVerifierTests
{
    private static readonly DateTime T0 = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Builds one candle per pivot (its extreme = the pivot price) and the matching annotations.</summary>
    private static (IReadOnlyList<WaveAnnotation> Annotations, IReadOnlyList<MarketCandle> Candles) Build(
        params (int Day, decimal Price, bool IsHigh, string Label)[] pivots)
    {
        var candles = new List<MarketCandle>();
        var annotations = new List<WaveAnnotation>();
        foreach (var (day, price, isHigh, label) in pivots)
        {
            var date = T0.AddDays(day);
            var high = isHigh ? price : price * 1.01m;
            var low = isHigh ? price * 0.99m : price;
            candles.Add(new MarketCandle(date, price, high, low, price, 0m));
            annotations.Add(new WaveAnnotation(date, price, label));
        }

        return (annotations, candles);
    }

    private static (int, decimal, bool, string)[] CleanImpulse() =>
    [
        (0, 100m, false, "0"), (10, 130m, true, "1"), (20, 115m, false, "2"),
        (30, 175m, true, "3"), (40, 150m, false, "4"), (50, 200m, true, "5"),
    ];

    [Test]
    public void Verify_CleanImpulse_IsValidWithLevelsAndScore()
    {
        var (annotations, candles) = Build(CleanImpulse());

        var result = WaveVerifier.Verify(annotations, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Structure, Is.EqualTo("Impulse"));
            Assert.That(result.Bullish, Is.True);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Snapped, Has.Count.EqualTo(6));
            Assert.That(result.Rejected, Is.Empty);
            Assert.That(result.Levels, Is.Not.Null);
            Assert.That(result.Score, Is.Not.Null);
        });
    }

    [Test]
    public void Verify_Wave2RetracesBeyondOrigin_InvalidatesWithAFailedHardRule()
    {
        // Wave 2 (day 20) drops to 95, below the origin (100) → Rule 1 fails.
        var (annotations, candles) = Build(
            (0, 100m, false, "0"), (10, 130m, true, "1"), (20, 95m, false, "2"),
            (30, 175m, true, "3"), (40, 150m, false, "4"), (50, 200m, true, "5"));

        var result = WaveVerifier.Verify(annotations, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Rules.Rules.Any(r => r is { Status: RuleStatus.Fail, IsGuideline: false }),
                Is.True);
        });
    }

    [Test]
    public void Verify_PivotOffEveryCandle_IsRejectedNotTrusted()
    {
        var (annotations, candles) = Build(CleanImpulse());
        // Add a pivot far from any candle extreme (and far in time) → cannot snap.
        var stray = annotations.Append(new WaveAnnotation(T0.AddDays(200), 9_999m, "5")).ToList();

        var result = WaveVerifier.Verify(stray, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Rejected, Is.Not.Empty);
            Assert.That(result.Snapped, Has.Count.EqualTo(6), "only the six real pivots snapped");
        });
    }

    [Test]
    public void Verify_IsDeterministic_SameInputsSameResult()
    {
        var (annotations, candles) = Build(CleanImpulse());

        var a = WaveVerifier.Verify(annotations, candles);
        var b = WaveVerifier.Verify(annotations, candles);

        // Record equality compares the List members by reference, so compare the serialized content.
        Assert.That(JsonSerializer.Serialize(a), Is.EqualTo(JsonSerializer.Serialize(b)));
    }

    [Test]
    public void Verify_CorrectiveZigzag_IsScoredAsCorrective()
    {
        // 0-A-B-C zigzag down: origin high, A low, B high (below origin), C low (below A).
        var (annotations, candles) = Build(
            (0, 200m, true, "0"), (10, 150m, false, "A"),
            (20, 175m, true, "B"), (30, 120m, false, "C"));

        var result = WaveVerifier.Verify(annotations, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Structure, Is.EqualTo("Corrective"));
            Assert.That(result.Snapped, Has.Count.EqualTo(4));
            Assert.That(result.Score, Is.Not.Null);
        });
    }

    [Test]
    public void Verify_TooFewPivots_StillReturnsAReportWithoutScore()
    {
        var (annotations, candles) = Build((0, 100m, false, "0"), (10, 130m, true, "1"));

        var result = WaveVerifier.Verify(annotations, candles);

        Assert.Multiple(() =>
        {
            Assert.That(result.Snapped, Has.Count.EqualTo(2));
            Assert.That(result.Score, Is.Null);
        });
    }
}
