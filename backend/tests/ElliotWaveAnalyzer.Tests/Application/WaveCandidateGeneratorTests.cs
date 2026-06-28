using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Unit tests for <see cref="WaveCandidateGenerator"/>. Pivots are fed directly so the test
/// exercises candidate generation independently of the swing detector.
/// </summary>
[TestFixture]
public sealed class WaveCandidateGeneratorTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static SwingPivot Low(int day, decimal price) => new(Start.AddDays(day), price, IsHigh: false);
    private static SwingPivot High(int day, decimal price) => new(Start.AddDays(day), price, IsHigh: true);

    [Test]
    public void FewerThanSixPivots_NoCandidates()
    {
        var pivots = new[] { Low(0, 100m), High(1, 120m), Low(2, 110m) };

        Assert.That(WaveCandidateGenerator.Generate(pivots), Is.Empty);
    }

    [Test]
    public void ValidBullishImpulse_ProducesOneCandidate()
    {
        // origin 100 → 1:120 → 2:110 → 3:150 → 4:130 → 5:170  (all three rules pass)
        var pivots = new[]
        {
            Low(0, 100m), High(1, 120m), Low(2, 110m), High(3, 150m), Low(4, 130m), High(5, 170m),
        };

        var candidates = WaveCandidateGenerator.Generate(pivots);

        Assert.That(candidates, Has.Count.EqualTo(1));
        var c = candidates[0];
        Assert.Multiple(() =>
        {
            Assert.That(c.Structure, Is.EqualTo("Impulse"));
            Assert.That(c.Id, Is.EqualTo(0));
            Assert.That(c.Origin.Price, Is.EqualTo(100m));
            Assert.That(c.Waves.Select(w => w.Label), Is.EqualTo(new[] { "1", "2", "3", "4", "5" }));
            Assert.That(c.Waves[^1].Price, Is.EqualTo(170m));
            Assert.That(c.RuleReport.Rules.Any(r => r.Status == RuleStatus.Fail), Is.False);
        });
    }

    [Test]
    public void Wave3Shortest_CandidateRejected()
    {
        // wave1=30, wave3=25, wave5=68 → Rule 2 fails, so no candidate survives.
        var pivots = new[]
        {
            Low(0, 100m), High(1, 130m), Low(2, 115m), High(3, 140m), Low(4, 132m), High(5, 200m),
        };

        Assert.That(WaveCandidateGenerator.Generate(pivots), Is.Empty);
    }

    [Test]
    public void MultipleWindows_AreRankedMostRecentFirst_AndReindexed()
    {
        // Seven alternating pivots → two overlapping 6-pivot windows, both rule-valid.
        var pivots = new[]
        {
            Low(0, 100m), High(1, 120m), Low(2, 110m), High(3, 150m),
            Low(4, 130m), High(5, 170m), Low(6, 150m),
        };

        var candidates = WaveCandidateGenerator.Generate(pivots);

        // Window starting at the second pivot ends later, so it must come first and be id 0.
        Assert.That(candidates, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(candidates[0].Id, Is.EqualTo(0));
        for (var i = 1; i < candidates.Count; i++)
        {
            Assert.That(candidates[i].Waves[^1].Date,
                Is.LessThanOrEqualTo(candidates[i - 1].Waves[^1].Date));
            Assert.That(candidates[i].Id, Is.EqualTo(i));
        }
    }
}
