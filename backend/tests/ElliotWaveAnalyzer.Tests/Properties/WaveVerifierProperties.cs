using System.Text.Json;
using CsCheck;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Property-based invariants for <see cref="WaveVerifier"/>: it never throws, its snapped pivots always
/// sit on real candle data (the hallucination guard), validity is exactly "no hard rule failed", and the
/// whole read is deterministic (I2) — the same inputs always serialize identically.
/// </summary>
[TestFixture]
public sealed class WaveVerifierProperties
{
    [Test]
    public void Verify_NeverThrows_AndEverySnappedPivotIsARealCandleExtreme()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            var r = WaveVerifier.Verify(s.Annotations, s.Candles);

            var extremes = s.Candles.SelectMany(c => new[] { c.High, c.Low }).ToHashSet();
            foreach (var pivot in r.Snapped)
            {
                Assert.That(extremes.Contains(pivot.Price), Is.True);
            }
        });
    }

    [Test]
    public void Verify_IsValid_IffNoHardRuleFailed()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            var r = WaveVerifier.Verify(s.Annotations, s.Candles);

            var hardFail = r.Rules.Rules.Any(rule => rule is { Status: RuleStatus.Fail, IsGuideline: false });
            Assert.That(r.IsValid, Is.EqualTo(!hardFail));
        });
    }

    [Test]
    public void Verify_IsDeterministic()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            var a = JsonSerializer.Serialize(WaveVerifier.Verify(s.Annotations, s.Candles));
            var b = JsonSerializer.Serialize(WaveVerifier.Verify(s.Annotations, s.Candles));
            Assert.That(a, Is.EqualTo(b));
        });
    }
}
