using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Pins AC3 (#189): the hard Elliott rules are immutable to the reflection loop. Structurally
/// guaranteed — <see cref="MissSetCalculator"/> and <see cref="LessonFactGuard"/> never take a
/// <see cref="WaveAnnotation"/>/<see cref="WaveRuleReport"/> as input, only tracked-analysis records
/// and case ids — so there is no reference through which they could influence
/// <see cref="ElliottRuleChecker"/>'s output. Regression-tests that against a live rule-checker
/// fixture, the way <see cref="ContextNonInterferenceTests"/> pins #188's equivalent claim against
/// <see cref="WaveLevels"/>.
/// </summary>
[TestFixture]
public sealed class LessonNonInterferenceTests
{
    private static IReadOnlyList<WaveAnnotation> FullImpulse() =>
    [
        new(new DateTime(2024, 1, 1), 100m, "1"),
        new(new DateTime(2024, 1, 2), 100m, "1"),
        new(new DateTime(2024, 1, 3), 110m, "2"),
        new(new DateTime(2024, 1, 4), 105m, "3"),
        new(new DateTime(2024, 1, 5), 130m, "4"),
        new(new DateTime(2024, 1, 6), 120m, "5"),
    ];

    [Test]
    public void ComputingAMissSetAndCheckingALesson_NeverChangesTheHardRuleCheckerOutput()
    {
        var annotations = FullImpulse();
        var before = JsonSerializer.Serialize(ElliottRuleChecker.Check(annotations));

        var missId = Guid.NewGuid();
        var analyses = new[]
        {
            new TrackedAnalysis(
                missId, "BTC", DateTimeOffset.UtcNow, "Impulse", true, 100m, false, 200m, 210m,
                "High", 0.8m, AnalysisOutcome.Invalidated, null, null),
        };

        var misses = MissSetCalculator.Compute(analyses);
        var lesson = new Lesson("shallow-wave-2", "hypothesis", [missId], 0.1m);
        _ = LessonFactGuard.Passes(lesson, misses.Select(m => m.Id).ToList());

        var after = JsonSerializer.Serialize(ElliottRuleChecker.Check(annotations));
        Assert.That(after, Is.EqualTo(before));
    }
}
