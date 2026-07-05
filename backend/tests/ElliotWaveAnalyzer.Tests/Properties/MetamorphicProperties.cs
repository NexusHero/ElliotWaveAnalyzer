using CsCheck;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Metamorphic invariants — relations that must hold when an input is transformed, catching whole
/// classes of bugs without a known-good oracle: the deterministic verdict is invariant under a positive
/// price scaling and under a time shift (a count is about structure, not absolute price or absolute dates).
/// </summary>
[TestFixture]
public sealed class MetamorphicProperties
{
    private static IReadOnlyList<RuleStatus> Statuses(WaveVerification v) =>
        v.Rules.Rules.Select(r => r.Status).ToList();

    [Test]
    public void RuleVerdict_IsInvariantUnderPositivePriceScaling()
    {
        Gen.Select(PropertyGenerators.Scenarios, Gen.Double[0.1, 50.0]).Sample(pair =>
        {
            var (s, kDouble) = pair;
            var k = (decimal)kDouble;

            var scaledCandles = s.Candles
                .Select(c => new MarketCandle(c.OpenTime, c.Open * k, c.High * k, c.Low * k, c.Close * k, c.Volume))
                .ToList();
            var scaledAnnotations = s.Annotations
                .Select(a => a with { Price = a.Price * k })
                .ToList();

            var baseline = WaveVerifier.Verify(s.Annotations, s.Candles);
            var scaled = WaveVerifier.Verify(scaledAnnotations, scaledCandles);

            Assert.Multiple(() =>
            {
                Assert.That(scaled.Structure, Is.EqualTo(baseline.Structure));
                Assert.That(scaled.IsValid, Is.EqualTo(baseline.IsValid));
                Assert.That(Statuses(scaled), Is.EqualTo(Statuses(baseline)));
            });
        });
    }

    [Test]
    public void RuleVerdict_IsInvariantUnderTimeShift()
    {
        Gen.Select(PropertyGenerators.Scenarios, Gen.Int[1, 500]).Sample(pair =>
        {
            var (s, days) = pair;

            var shiftedCandles = s.Candles.Select(c => c with { OpenTime = c.OpenTime.AddDays(days) }).ToList();
            var shiftedAnnotations = s.Annotations.Select(a => a with { Date = a.Date.AddDays(days) }).ToList();

            var baseline = WaveVerifier.Verify(s.Annotations, s.Candles);
            var shifted = WaveVerifier.Verify(shiftedAnnotations, shiftedCandles);

            Assert.Multiple(() =>
            {
                Assert.That(shifted.Structure, Is.EqualTo(baseline.Structure));
                Assert.That(shifted.IsValid, Is.EqualTo(baseline.IsValid));
                Assert.That(Statuses(shifted), Is.EqualTo(Statuses(baseline)));
            });
        });
    }
}
