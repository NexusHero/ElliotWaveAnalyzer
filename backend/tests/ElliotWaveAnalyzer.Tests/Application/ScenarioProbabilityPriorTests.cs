using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The backtest prior in <see cref="ScenarioProbability.From"/> (REQ-026): a thin personal record
/// falls back to the prior; a rich one blends toward it but stays led by the measured hit-rate; with
/// neither, the probability is withheld.
/// </summary>
[TestFixture]
public sealed class ScenarioProbabilityPriorTests
{
    private static CalibrationBucket Bucket(int concluded, decimal? hitRate) =>
        new("high", Total: concluded, Concluded: concluded, TargetReached: 0, Invalidated: 0, HitRate: hitRate);

    [Test]
    public void From_InsufficientSampleButPriorAvailable_UsesTheBacktestedPrior()
    {
        var estimate = ScenarioProbability.From(Bucket(concluded: 3, hitRate: null), backtestPrior: 0.42m);

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.Backtested));
            Assert.That(estimate.Probability, Is.EqualTo(0.42m));
        });
    }

    [Test]
    public void From_EnoughSample_BlendsMeasuredTowardThePrior_MeasuredLeads()
    {
        // measured 0.6, prior 0.4 → 0.7*0.6 + 0.3*0.4 = 0.54.
        var estimate = ScenarioProbability.From(Bucket(concluded: 20, hitRate: 0.6m), backtestPrior: 0.4m);

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.Calibrated));
            Assert.That(estimate.Probability!.Value, Is.EqualTo(0.54m).Within(1e-9m));
        });
    }

    [Test]
    public void From_EnoughSampleNoPrior_ReturnsMeasuredUnchanged()
    {
        var estimate = ScenarioProbability.From(Bucket(concluded: 20, hitRate: 0.6m));

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.Calibrated));
            Assert.That(estimate.Probability, Is.EqualTo(0.6m));
        });
    }

    [Test]
    public void From_NeitherSampleNorPrior_IsInsufficientData()
    {
        var estimate = ScenarioProbability.From(Bucket(concluded: 2, hitRate: null));

        Assert.Multiple(() =>
        {
            Assert.That(estimate.Basis, Is.EqualTo(ProbabilityBasis.InsufficientData));
            Assert.That(estimate.Probability, Is.Null);
        });
    }
}
