using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="MissSetCalculator"/>: deterministically computes the miss set (concluded, invalidated
/// analyses) from a track record — the evidence base #189's lessons must cite (AC1).
/// </summary>
[TestFixture]
public sealed class MissSetCalculatorTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TrackedAnalysis Analysis(Guid id, AnalysisOutcome outcome, string confidence = "High") => new(
        id, "BTC", CreatedAt, "Impulse", true, 100m, false, 200m, 210m, confidence, 0.8m, outcome, null, null);

    [Test]
    public void Compute_InvalidatedAnalysis_IsIncludedAsAMiss()
    {
        var id = Guid.NewGuid();
        var misses = MissSetCalculator.Compute([Analysis(id, AnalysisOutcome.Invalidated)]);

        Assert.Multiple(() =>
        {
            Assert.That(misses, Has.Count.EqualTo(1));
            Assert.That(misses[0].Id, Is.EqualTo(id));
            Assert.That(misses[0].Symbol, Is.EqualTo("BTC"));
            Assert.That(misses[0].Structure, Is.EqualTo("Impulse"));
            Assert.That(misses[0].Confidence, Is.EqualTo("high"));
        });
    }

    [Test]
    public void Compute_TargetReachedAnalysis_IsExcluded_NotAMiss()
    {
        var misses = MissSetCalculator.Compute([Analysis(Guid.NewGuid(), AnalysisOutcome.TargetReached)]);
        Assert.That(misses, Is.Empty);
    }

    [Test]
    public void Compute_PendingAnalysis_IsExcluded()
    {
        // AC1: an unsettled call is neither a hit nor a miss yet.
        var misses = MissSetCalculator.Compute([Analysis(Guid.NewGuid(), AnalysisOutcome.Pending)]);
        Assert.That(misses, Is.Empty);
    }

    [Test]
    public void Compute_MixedOutcomes_OnlyInvalidatedSurvive()
    {
        var invalidatedId = Guid.NewGuid();
        var misses = MissSetCalculator.Compute([
            Analysis(Guid.NewGuid(), AnalysisOutcome.Pending),
            Analysis(invalidatedId, AnalysisOutcome.Invalidated),
            Analysis(Guid.NewGuid(), AnalysisOutcome.TargetReached),
        ]);

        Assert.That(misses.Select(m => m.Id), Is.EquivalentTo(new[] { invalidatedId }));
    }

    [Test]
    public void Compute_NormalizesConfidenceCaseInsensitively()
    {
        var misses = MissSetCalculator.Compute([Analysis(Guid.NewGuid(), AnalysisOutcome.Invalidated, "  HIGH  ")]);
        Assert.That(misses[0].Confidence, Is.EqualTo("high"));
    }

    [Test]
    public void Compute_BlankConfidence_NormalizesToUnknown()
    {
        var misses = MissSetCalculator.Compute([Analysis(Guid.NewGuid(), AnalysisOutcome.Invalidated, "")]);
        Assert.That(misses[0].Confidence, Is.EqualTo("unknown"));
    }

    [Test]
    public void Compute_SameAnalysesTwice_ProducesTheSameMissSet()
    {
        // Determinism (AC1): repeated computation over the same input is stable.
        var analyses = new[] { Analysis(Guid.NewGuid(), AnalysisOutcome.Invalidated) };
        var first = MissSetCalculator.Compute(analyses);
        var second = MissSetCalculator.Compute(analyses);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Compute_NullAnalyses_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MissSetCalculator.Compute(null!));
    }
}
