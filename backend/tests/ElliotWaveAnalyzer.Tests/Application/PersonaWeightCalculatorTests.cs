using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="PersonaWeightCalculator"/>: weight from measured hit-rate, the documented neutral prior
/// for a persona with no concluded history (AC3), and pending outcomes never moving a weight (AC6).
/// </summary>
[TestFixture]
public sealed class PersonaWeightCalculatorTests
{
    private static (string, IReadOnlyList<(string, AnalysisOutcome)>) Persona(
        string name, params (string Confidence, AnalysisOutcome Outcome)[] outcomes) => (name, outcomes);

    [Test]
    public void Calculate_PersonaWithConcludedHistory_WeightIsItsMeasuredHitRate()
    {
        var result = PersonaWeightCalculator.Calculate(
        [
            Persona("Conservative",
                ("high", AnalysisOutcome.TargetReached),
                ("high", AnalysisOutcome.TargetReached),
                ("high", AnalysisOutcome.Invalidated)),
        ]);

        var weight = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(weight.Persona, Is.EqualTo("Conservative"));
            Assert.That(weight.Weight, Is.EqualTo(2.0 / 3.0).Within(0.001));
            Assert.That(weight.IsNeutralPrior, Is.False);
        });
    }

    [Test]
    public void Calculate_PersonaWithNoHistory_UsesTheDocumentedNeutralPrior()
    {
        var result = PersonaWeightCalculator.Calculate([Persona("Aggressive")]);

        var weight = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(weight.Weight, Is.EqualTo(PersonaWeightCalculator.NeutralPrior));
            Assert.That(weight.IsNeutralPrior, Is.True);
        });
    }

    [Test]
    public void Calculate_PersonaWithOnlyPendingHistory_UsesTheNeutralPrior_NotAFabricatedWeight()
    {
        // A pending pick has no concluded outcome yet — it must not move the weight (AC6).
        var result = PersonaWeightCalculator.Calculate(
        [
            Persona("Contrarian", ("medium", AnalysisOutcome.Pending), ("medium", AnalysisOutcome.Pending)),
        ]);

        var weight = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(weight.Weight, Is.EqualTo(PersonaWeightCalculator.NeutralPrior));
            Assert.That(weight.IsNeutralPrior, Is.True);
        });
    }

    [Test]
    public void Calculate_MultiplePersonas_OneWeightEachInGivenOrder()
    {
        var result = PersonaWeightCalculator.Calculate(
        [
            Persona("Conservative", ("high", AnalysisOutcome.TargetReached)),
            Persona("Aggressive"),
            Persona("Contrarian", ("low", AnalysisOutcome.Invalidated)),
        ]);

        Assert.That(result.Select(w => w.Persona), Is.EqualTo(new[] { "Conservative", "Aggressive", "Contrarian" }));
    }

    [Test]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PersonaWeightCalculator.Calculate(null!));
    }
}
