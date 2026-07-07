using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// The deterministic sentiment normalizer: reproducible, clamped to [-1, 1], sorted by date (AC1).
/// </summary>
[TestFixture]
public sealed class SentimentIndexBuilderTests
{
    [Test]
    public void Normalize_SameInputTwice_IsByteIdentical()
    {
        var raw = new[]
        {
            new SentimentPoint(new DateTime(2024, 1, 2), 0.4),
            new SentimentPoint(new DateTime(2024, 1, 1), -0.3),
        };

        var first = SentimentIndexBuilder.Normalize(raw);
        var second = SentimentIndexBuilder.Normalize(raw);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Normalize_OutOfRangeScores_AreClamped()
    {
        var raw = new[]
        {
            new SentimentPoint(new DateTime(2024, 1, 1), 4.2),
            new SentimentPoint(new DateTime(2024, 1, 2), -9.0),
        };

        var result = SentimentIndexBuilder.Normalize(raw);

        Assert.Multiple(() =>
        {
            Assert.That(result[0].Score, Is.EqualTo(1.0));
            Assert.That(result[1].Score, Is.EqualTo(-1.0));
        });
    }

    [Test]
    public void Normalize_UnsortedInput_IsSortedByDate()
    {
        var raw = new[]
        {
            new SentimentPoint(new DateTime(2024, 3, 1), 0.1),
            new SentimentPoint(new DateTime(2024, 1, 1), 0.2),
            new SentimentPoint(new DateTime(2024, 2, 1), 0.3),
        };

        var result = SentimentIndexBuilder.Normalize(raw);

        Assert.That(result.Select(p => p.Date), Is.Ordered);
    }

    [Test]
    public void Normalize_Empty_ReturnsEmpty()
    {
        Assert.That(SentimentIndexBuilder.Normalize([]), Is.Empty);
    }
}
