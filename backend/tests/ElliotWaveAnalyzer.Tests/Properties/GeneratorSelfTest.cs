using CsCheck;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Proves the generators only ever emit <b>valid</b> fixtures (ADR-034 AC2), so a property failure is
/// always a real defect in the code under test, never a bad input.
/// </summary>
[TestFixture]
public sealed class GeneratorSelfTest
{
    [Test]
    public void Scenarios_ProduceOnlyValidCandlesAndOnCandleAnnotations()
    {
        PropertyGenerators.Scenarios.Sample(s =>
        {
            foreach (var c in s.Candles)
            {
                Assert.That(c.Low, Is.LessThanOrEqualTo(c.High));
                Assert.That(c.Open, Is.GreaterThanOrEqualTo(c.Low).And.LessThanOrEqualTo(c.High));
                Assert.That(c.Close, Is.GreaterThanOrEqualTo(c.Low).And.LessThanOrEqualTo(c.High));
            }

            var extremes = s.Candles.SelectMany(c => new[] { c.High, c.Low }).ToHashSet();
            Assert.That(s.Annotations, Is.Not.Empty);
            foreach (var a in s.Annotations)
            {
                Assert.That(extremes.Contains(a.Price), Is.True, "every annotation sits on a real candle extreme");
            }
        });
    }
}
