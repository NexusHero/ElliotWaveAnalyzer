using CsCheck;
using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Properties;

/// <summary>
/// Property-based invariants for <see cref="RiskCalculator"/> (I9 financial-math + I2 determinism):
/// over thousands of generated inputs the sizing can never go negative/blow up, the no-valid-stop
/// verdict is exactly the direction guard, and the size reconstructs the risked capital.
/// </summary>
[TestFixture]
public sealed class RiskCalculatorProperties
{
    private static readonly Gen<double> Price = Gen.Double[0.01, 100_000.0];

    // Risk capital of at least one currency unit — a sub-cent risk is not a real scenario, and it would
    // only manufacture a decimal-precision underflow (size → 0) that says nothing about the logic. The
    // zero/negative-risk branch is covered by the example-based RiskCalculatorTests.
    private static readonly Gen<double> Amount = Gen.Double[1.0, 1_000_000.0];
    private static readonly Gen<double[]> Targets = Gen.Double[0.01, 200_000.0].Array[0, 4];

    private static readonly Gen<(double entry, double inval, bool bullish, double cap, double[] targets)> Inputs =
        Gen.Select(Price, Price, Gen.Bool, Amount)
            .SelectMany(core => Targets.Select(t => (core.Item1, core.Item2, core.Item3, core.Item4, t)));

    [Test]
    public void Assess_NeverProducesANegativeOrZeroSizeOrNotional()
    {
        Inputs.Sample(x =>
        {
            var (entry, inval, bullish, cap, targets) = x;
            var r = RiskCalculator.Assess(
                (decimal)entry, (decimal)inval, targets.Select(t => (decimal)t).ToList(), bullish, (decimal)cap);

            if (r.SuggestedSize is { } size)
            {
                Assert.That(size, Is.GreaterThan(0m));
            }

            if (r.Notional is { } notional)
            {
                Assert.That(notional, Is.GreaterThan(0m));
            }
        });
    }

    [Test]
    public void Assess_HasValidStop_IffEntryIsOnTheCorrectSideOfTheInvalidation()
    {
        Inputs.Sample(x =>
        {
            var (entry, inval, bullish, cap, targets) = x;
            var r = RiskCalculator.Assess(
                (decimal)entry, (decimal)inval, targets.Select(t => (decimal)t).ToList(), bullish, (decimal)cap);

            var validExpected = bullish ? (decimal)inval < (decimal)entry : (decimal)inval > (decimal)entry;
            Assert.That(r.HasValidStop, Is.EqualTo(validExpected));
            if (!r.HasValidStop)
            {
                Assert.That(r.SuggestedSize, Is.Null);
            }
        });
    }

    [Test]
    public void Assess_WithAValidStopAndPositiveRisk_SizeReconstructsTheRiskedCapital()
    {
        Inputs.Sample(x =>
        {
            var (entry, inval, bullish, cap, targets) = x;
            var r = RiskCalculator.Assess(
                (decimal)entry, (decimal)inval, targets.Select(t => (decimal)t).ToList(), bullish, (decimal)cap);

            if (r.HasValidStop && (decimal)cap > 0m)
            {
                Assert.That(r.SuggestedSize, Is.EqualTo((decimal)cap / r.StopDistanceAbs));
            }
        });
    }

    [Test]
    public void Assess_TargetsAreReportedAscendingByPrice()
    {
        Inputs.Sample(x =>
        {
            var (entry, inval, bullish, cap, targets) = x;
            var r = RiskCalculator.Assess(
                (decimal)entry, (decimal)inval, targets.Select(t => (decimal)t).ToList(), bullish, (decimal)cap);

            Assert.That(r.Targets.Select(t => t.Price), Is.Ordered);
        });
    }
}
