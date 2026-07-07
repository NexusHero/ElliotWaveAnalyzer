using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="TutorLessonAssembler"/>: assembles a <see cref="TutorLesson"/> whose per-rule verdicts
/// always come from the engine's own <see cref="WaveRuleReport"/>, never an LLM draft (#190, AC1, AC2,
/// AC6).
/// </summary>
[TestFixture]
public sealed class TutorLessonAssemblerTests
{
    private static WaveRuleReport Report(RuleStatus rule2Status = RuleStatus.Pass) => new(
        BullishAssumed: true,
        Rules:
        [
            new RuleResult("Rule 1 — Wave 2 stays within Wave 1's origin", RuleStatus.Pass, "Wave 2 holds above the origin."),
            new RuleResult("Rule 2 — Wave 3 is not the shortest impulse wave", rule2Status,
                rule2Status == RuleStatus.Fail ? "Wave 3 is the shortest of waves 1/3/5." : "Wave 3 is not the shortest."),
        ],
        Ratios: []);

    [Test]
    public void Assemble_NoDraft_FallsBackToTheEngineDetailForEveryRule()
    {
        var lesson = TutorLessonAssembler.Assemble(Report(), draft: null);

        Assert.Multiple(() =>
        {
            Assert.That(lesson.RuleExplanations, Has.Count.EqualTo(2));
            Assert.That(lesson.RuleExplanations[0].Explanation, Is.EqualTo("Wave 2 holds above the origin."));
            Assert.That(lesson.RuleExplanations[1].Explanation, Is.EqualTo("Wave 3 is not the shortest."));
            Assert.That(lesson.Summary, Is.Null);
            Assert.That(lesson.SummaryUnavailableReason, Is.Not.Null);
        });
    }

    [Test]
    public void Assemble_DraftWithMatchingNarrative_UsesTheLlmExplanation()
    {
        var draft = new TutorExplanationDraft(
            [new TutorRuleNarrative("Rule 1 — Wave 2 stays within Wave 1's origin", "Think of it as the floor wave 1 built.")],
            Summary: "A clean five-wave impulse.");

        var lesson = TutorLessonAssembler.Assemble(Report(), draft);

        Assert.Multiple(() =>
        {
            Assert.That(lesson.RuleExplanations[0].Explanation, Is.EqualTo("Think of it as the floor wave 1 built."));
            Assert.That(lesson.Summary, Is.EqualTo("A clean five-wave impulse."));
            Assert.That(lesson.SummaryUnavailableReason, Is.Null);
        });
    }

    [Test]
    public void Assemble_DraftNarrativeForAnUnknownRuleName_IsIgnored_UnmatchedRulesStillUseEngineDetail()
    {
        var draft = new TutorExplanationDraft([new TutorRuleNarrative("Not a real rule", "fabricated")], null);

        var lesson = TutorLessonAssembler.Assemble(Report(), draft);

        Assert.That(lesson.RuleExplanations[0].Explanation, Is.EqualTo("Wave 2 holds above the origin."));
    }

    [Test]
    public void Assemble_StatusIsAlwaysTheEnginesOwnVerdict_RegardlessOfAnyDraftContent()
    {
        // AC1/AC2: even a maximally "helpful" draft narrative cannot flip the shown verdict — Status
        // has no channel on the draft type at all.
        var draft = new TutorExplanationDraft(
            [new TutorRuleNarrative("Rule 2 — Wave 3 is not the shortest impulse wave", "This rule definitely passes!")],
            null);

        var lesson = TutorLessonAssembler.Assemble(Report(rule2Status: RuleStatus.Fail), draft);

        Assert.That(lesson.RuleExplanations[1].Status, Is.EqualTo(RuleStatus.Fail));
    }

    [Test]
    public void Assemble_NoHardRuleFails_IsValidTrue()
    {
        var lesson = TutorLessonAssembler.Assemble(Report(rule2Status: RuleStatus.Pass), null);
        Assert.That(lesson.IsValid, Is.True);
    }

    [Test]
    public void Assemble_AHardRuleFails_IsValidFalse()
    {
        // AC2 explicit: a rule-violating count must be reported as invalid, matching the engine.
        var lesson = TutorLessonAssembler.Assemble(Report(rule2Status: RuleStatus.Fail), null);
        Assert.That(lesson.IsValid, Is.False);
    }

    [Test]
    public void Assemble_IndeterminateRule_DoesNotAffectValidity()
    {
        var lesson = TutorLessonAssembler.Assemble(Report(rule2Status: RuleStatus.Indeterminate), null);
        Assert.That(lesson.IsValid, Is.True);
    }

    [Test]
    public void Assemble_BlankSummary_IsTreatedAsUnavailable()
    {
        var draft = new TutorExplanationDraft(null, "   ");
        var lesson = TutorLessonAssembler.Assemble(Report(), draft);

        Assert.Multiple(() =>
        {
            Assert.That(lesson.Summary, Is.Null);
            Assert.That(lesson.SummaryUnavailableReason, Is.Not.Null);
        });
    }

    [Test]
    public void Assemble_NullReport_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TutorLessonAssembler.Assemble(null!, null));
    }
}
