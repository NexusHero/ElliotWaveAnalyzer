using System.Text.Json;
using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// Pins AC3 (#190): the tutor is read-only — it never changes the count, pivots, or levels.
/// Structurally guaranteed — <see cref="TutorLessonAssembler"/>, <see cref="TutorFactGuard"/> and
/// <see cref="TutorQuizChecker"/> only ever read a <see cref="WaveRuleReport"/> (already computed,
/// immutable) — never a mutable count/pivot collection — so there is no reference through which a
/// tutor interaction could change the annotations or the rule checker's own output. Regression-tests
/// that against a live <see cref="ElliottRuleChecker"/> fixture, the same pinning style
/// <see cref="LessonNonInterferenceTests"/> uses for #189's rule-immutability claim.
/// </summary>
[TestFixture]
public sealed class TutorNonInterferenceTests
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
    public void ExplainingAndQuizzing_NeverChangesTheAnnotationsOrTheRuleCheckerOutput()
    {
        var annotations = FullImpulse();
        var annotationsBefore = JsonSerializer.Serialize(annotations);
        var report = ElliottRuleChecker.Check(annotations);
        var reportBefore = JsonSerializer.Serialize(report);

        var draft = new TutorExplanationDraft(
            [new TutorRuleNarrative(report.Rules[0].Name, "an explanation")], "a summary");
        _ = TutorFactGuard.Passes(draft, report);
        _ = TutorLessonAssembler.Assemble(report, draft);
        _ = TutorQuizChecker.CheckAnswer(report, report.Rules[0].Name, report.Rules[0].Status);

        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(annotations), Is.EqualTo(annotationsBefore));
            Assert.That(JsonSerializer.Serialize(ElliottRuleChecker.Check(annotations)), Is.EqualTo(reportBefore));
        });
    }
}
