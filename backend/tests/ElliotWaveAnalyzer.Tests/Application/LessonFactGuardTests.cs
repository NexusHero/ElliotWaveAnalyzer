using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="LessonFactGuard"/>: a lesson may only cite real recorded misses. A lesson citing even
/// one non-existent case id is rejected wholesale; an unsupported (no-case) lesson is rejected too;
/// an oversized weight nudge is rejected (#189, AC2).
/// </summary>
[TestFixture]
public sealed class LessonFactGuardTests
{
    private static readonly Guid Real1 = Guid.NewGuid();
    private static readonly Guid Real2 = Guid.NewGuid();
    private static readonly Guid Fabricated = Guid.NewGuid();

    private static IReadOnlyCollection<Guid> RealCaseIds => [Real1, Real2];

    [Test]
    public void Passes_LessonCitingOnlyRealCaseIds_IsAllowed()
    {
        var lesson = new Lesson("shallow-wave-2", "Shallow wave 2s in low volatility tend to fail.", [Real1, Real2], 0.05m);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.True);
    }

    [Test]
    public void Passes_LessonCitingANonExistentCaseId_IsRejectedWholesale()
    {
        // AC2: even one fabricated case id voids the whole lesson — a real case cited alongside it
        // does not partially save it.
        var lesson = new Lesson("shallow-wave-2", "hypothesis", [Real1, Fabricated], null);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.False);
    }

    [Test]
    public void Passes_LessonWithNoSupportingCases_IsRejected()
    {
        var lesson = new Lesson("category", "an unsupported claim", [], null);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.False);
    }

    [Test]
    public void Passes_WeightNudgeWithinBound_IsAllowed()
    {
        var lesson = new Lesson("category", "hypothesis", [Real1], LessonFactGuard.MaxWeightNudge);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.True);
    }

    [Test]
    public void Passes_WeightNudgeBeyondBound_IsRejected()
    {
        var lesson = new Lesson("category", "hypothesis", [Real1], LessonFactGuard.MaxWeightNudge + 0.01m);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.False);
    }

    [Test]
    public void Passes_NegativeWeightNudgeBeyondBound_IsRejected()
    {
        var lesson = new Lesson("category", "hypothesis", [Real1], -(LessonFactGuard.MaxWeightNudge + 0.01m));
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.False);
    }

    [Test]
    public void Passes_NoWeightNudgeProposed_IsAllowed()
    {
        var lesson = new Lesson("category", "hypothesis", [Real1], null);
        Assert.That(LessonFactGuard.Passes(lesson, RealCaseIds), Is.True);
    }

    [Test]
    public void Passes_NullLessonOrRealCaseIds_Throws()
    {
        var lesson = new Lesson("category", "hypothesis", [Real1], null);
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => LessonFactGuard.Passes(null!, RealCaseIds));
            Assert.Throws<ArgumentNullException>(() => LessonFactGuard.Passes(lesson, null!));
        });
    }
}
