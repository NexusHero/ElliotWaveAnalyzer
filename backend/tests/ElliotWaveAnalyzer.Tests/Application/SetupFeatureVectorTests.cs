using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The feature-vector encoding: deterministic, self-similarity of exactly 1, cosine bounded in
/// [0, 1], and a wrong pattern family / direction pulling similarity below same-shape neighbours.
/// </summary>
[TestFixture]
public sealed class SetupFeatureVectorTests
{
    private static SetupFeatures Features(
        StructureKind structure = StructureKind.Impulse,
        bool bullish = true,
        double score = 0.7,
        double confluence = 0.5,
        double rewardToRisk = 2.0,
        double distance = 0.08,
        double rsi = 0.55,
        double macd = 0.6) =>
        new(structure, bullish, "1d", score, confluence, rewardToRisk, distance, rsi, macd);

    [Test]
    public void Encode_SameFeatures_ProducesIdenticalVector()
    {
        var a = SetupFeatureVector.Encode(Features());
        var b = SetupFeatureVector.Encode(Features());
        Assert.That(a, Is.EqualTo(b));
    }

    [Test]
    public void Similarity_IdenticalFeatures_IsOne()
    {
        Assert.That(SetupFeatureVector.Similarity(Features(), Features()), Is.EqualTo(1.0).Within(1e-9));
    }

    [Test]
    public void Cosine_IsBoundedInUnitInterval()
    {
        var impulse = SetupFeatureVector.Encode(Features(StructureKind.Impulse));
        var zigzag = SetupFeatureVector.Encode(Features(StructureKind.Zigzag, bullish: false));
        var sim = SetupFeatureVector.Cosine(impulse, zigzag);
        Assert.That(sim, Is.InRange(0.0, 1.0));
    }

    [Test]
    public void Similarity_WrongStructure_IsLessThanSameStructure()
    {
        var query = Features(StructureKind.Impulse);
        var sameShape = Features(StructureKind.Impulse, score: 0.6, confluence: 0.4);
        var otherShape = Features(StructureKind.Zigzag, score: 0.6, confluence: 0.4);

        Assert.That(
            SetupFeatureVector.Similarity(query, otherShape),
            Is.LessThan(SetupFeatureVector.Similarity(query, sameShape)));
    }

    [Test]
    public void Similarity_OppositeDirection_IsLessThanSameDirection()
    {
        var query = Features(bullish: true);
        Assert.That(
            SetupFeatureVector.Similarity(query, Features(bullish: false)),
            Is.LessThan(SetupFeatureVector.Similarity(query, Features(bullish: true))));
    }

    [Test]
    public void Cosine_LengthMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() => SetupFeatureVector.Cosine([1.0, 2.0], [1.0]));
    }
}
