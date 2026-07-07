using ElliotWaveAnalyzer.Api.Application;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="CorrelationCalculator"/>: pure Pearson-correlation math over aligned return series —
/// the deterministic "how correlated" computation behind the intermarket context overlay (#188).
/// </summary>
[TestFixture]
public sealed class CorrelationCalculatorTests
{
    [Test]
    public void Pearson_IdenticalSeries_ReturnsOne()
    {
        double[] a = [1, 2, 3, 4, 5];
        double[] b = [1, 2, 3, 4, 5];
        Assert.That(CorrelationCalculator.Pearson(a, b), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void Pearson_PerfectlyInverseSeries_ReturnsNegativeOne()
    {
        double[] a = [1, 2, 3, 4, 5];
        double[] b = [5, 4, 3, 2, 1];
        Assert.That(CorrelationCalculator.Pearson(a, b), Is.EqualTo(-1.0).Within(1e-9));
    }

    [Test]
    public void Pearson_ConstantSeries_ReturnsZero_NotDivideByZeroError()
    {
        // Zero variance in one series makes "correlation" undefined; 0.0 is the honest degenerate value.
        double[] a = [3, 3, 3, 3];
        double[] b = [1, 2, 3, 4];
        Assert.That(CorrelationCalculator.Pearson(a, b), Is.EqualTo(0.0));
    }

    [Test]
    public void Pearson_FewerThanTwoObservations_ReturnsZero()
    {
        Assert.That(CorrelationCalculator.Pearson([1.0], [2.0]), Is.EqualTo(0.0));
        Assert.That(CorrelationCalculator.Pearson([], []), Is.EqualTo(0.0));
    }

    [Test]
    public void Pearson_MismatchedLengths_Throws()
    {
        Assert.Throws<ArgumentException>(() => CorrelationCalculator.Pearson([1, 2, 3], [1, 2]));
    }

    [Test]
    public void Pearson_NullSeries_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => CorrelationCalculator.Pearson(null!, [1.0]));
            Assert.Throws<ArgumentNullException>(() => CorrelationCalculator.Pearson([1.0], null!));
        });
    }

    [Test]
    public void PercentReturns_KnownSeries_ComputesExpectedPercentages()
    {
        decimal[] closes = [100m, 110m, 99m];
        var returns = CorrelationCalculator.PercentReturns(closes);

        Assert.Multiple(() =>
        {
            Assert.That(returns, Has.Count.EqualTo(2));
            Assert.That(returns[0], Is.EqualTo(0.10).Within(1e-9)); // 100 -> 110
            Assert.That(returns[1], Is.EqualTo(-0.10).Within(1e-9)); // 110 -> 99
        });
    }

    [Test]
    public void PercentReturns_ZeroPreviousClose_ReturnsZeroForThatStep_NotDivideByZeroError()
    {
        decimal[] closes = [0m, 5m];
        var returns = CorrelationCalculator.PercentReturns(closes);
        Assert.That(returns[0], Is.EqualTo(0.0));
    }

    [Test]
    public void PercentReturns_NullSeries_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CorrelationCalculator.PercentReturns(null!));
    }
}
