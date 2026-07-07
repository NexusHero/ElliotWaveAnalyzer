using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="TutorFactGuard"/>: a tutor draft may only narrate rules that actually exist in the
/// report it's explaining (#190, AC1).
/// </summary>
[TestFixture]
public sealed class TutorFactGuardTests
{
    private static WaveRuleReport Report() => new(
        true,
        [
            new RuleResult("Rule 1 — Wave 2 stays within Wave 1's origin", RuleStatus.Pass, "detail"),
            new RuleResult("Rule 2 — Wave 3 is not the shortest impulse wave", RuleStatus.Fail, "detail"),
        ],
        []);

    [Test]
    public void Passes_NarrativesForRealRuleNames_IsAllowed()
    {
        var draft = new TutorExplanationDraft(
            [
                new TutorRuleNarrative("Rule 1 — Wave 2 stays within Wave 1's origin", "explanation"),
                new TutorRuleNarrative("Rule 2 — Wave 3 is not the shortest impulse wave", "explanation"),
            ], null);

        Assert.That(TutorFactGuard.Passes(draft, Report()), Is.True);
    }

    [Test]
    public void Passes_NarrativeForANonExistentRuleName_IsRejected()
    {
        var draft = new TutorExplanationDraft([new TutorRuleNarrative("Rule 99 — fabricated", "explanation")], null);
        Assert.That(TutorFactGuard.Passes(draft, Report()), Is.False);
    }

    [Test]
    public void Passes_OneRealAndOneFabricatedRuleName_IsRejected()
    {
        var draft = new TutorExplanationDraft(
            [
                new TutorRuleNarrative("Rule 1 — Wave 2 stays within Wave 1's origin", "explanation"),
                new TutorRuleNarrative("Rule 99 — fabricated", "explanation"),
            ], null);

        Assert.That(TutorFactGuard.Passes(draft, Report()), Is.False);
    }

    [Test]
    public void Passes_NoNarratives_IsAllowed()
    {
        Assert.That(TutorFactGuard.Passes(new TutorExplanationDraft(null, "summary only"), Report()), Is.True);
    }

    [Test]
    public void Passes_NullDraftOrReport_Throws()
    {
        var draft = new TutorExplanationDraft(null, null);
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => TutorFactGuard.Passes(null!, Report()));
            Assert.Throws<ArgumentNullException>(() => TutorFactGuard.Passes(draft, null!));
        });
    }
}
