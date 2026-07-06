using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The vocabulary guard (#186, AC1): only known Elliott structures pass; anything else is rejected
/// before it can reach generation, so the LLM can never introduce a structure the engine can't test.
/// </summary>
[TestFixture]
public sealed class StructureVocabularyTests
{
    [TestCase("impulse", StructureKind.Impulse)]
    [TestCase("Impulse", StructureKind.Impulse)]
    [TestCase("leading diagonal", StructureKind.Diagonal)]
    [TestCase("ending diagonal", StructureKind.Diagonal)]
    [TestCase("zigzag", StructureKind.Zigzag)]
    [TestCase("zig-zag", StructureKind.Zigzag)]
    [TestCase("expanded flat", StructureKind.Flat)]
    [TestCase("running flat", StructureKind.Flat)]
    [TestCase("contracting triangle", StructureKind.Triangle)]
    public void TryParse_KnownVocabulary_MapsToKind(string proposal, StructureKind expected)
    {
        Assert.That(StructureVocabulary.TryParse(proposal), Is.EqualTo(expected));
    }

    [TestCase("combination")]
    [TestCase("double three")]
    [TestCase("wave X")]
    [TestCase("head and shoulders")]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void TryParse_OutOfVocabulary_IsRejected(string? proposal)
    {
        Assert.That(StructureVocabulary.TryParse(proposal), Is.Null);
    }
}
