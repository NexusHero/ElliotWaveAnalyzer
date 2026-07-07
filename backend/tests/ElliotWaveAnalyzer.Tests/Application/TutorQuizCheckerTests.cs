using ElliotWaveAnalyzer.Api.Application;
using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Tests.Application;

/// <summary>
/// <see cref="TutorQuizChecker"/>: a quiz answer is checked against the deterministic rule report,
/// never the LLM's opinion (#190, AC4).
/// </summary>
[TestFixture]
public sealed class TutorQuizCheckerTests
{
    private const string Rule2Name = "Rule 2 — Wave 3 is not the shortest impulse wave";

    private static WaveRuleReport Report() => new(
        true,
        [
            new RuleResult("Rule 1 — Wave 2 stays within Wave 1's origin", RuleStatus.Pass, "detail"),
            new RuleResult(Rule2Name, RuleStatus.Fail, "detail"),
        ],
        []);

    [Test]
    public void CheckAnswer_CorrectAnswerMatchingTheEngine_ReturnsTrue()
    {
        Assert.That(TutorQuizChecker.CheckAnswer(Report(), Rule2Name, RuleStatus.Fail), Is.True);
    }

    [Test]
    public void CheckAnswer_WrongAnswerContradictingTheEngine_ReturnsFalse()
    {
        // AC4: the learner believes the rule passed, but the engine says it failed — marked wrong.
        Assert.That(TutorQuizChecker.CheckAnswer(Report(), Rule2Name, RuleStatus.Pass), Is.False);
    }

    [Test]
    public void CheckAnswer_UnknownRuleName_ReturnsFalse()
    {
        Assert.That(TutorQuizChecker.CheckAnswer(Report(), "Not a real rule", RuleStatus.Pass), Is.False);
    }

    [Test]
    public void CheckAnswer_NullReportOrRuleName_Throws()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentNullException>(() => TutorQuizChecker.CheckAnswer(null!, Rule2Name, RuleStatus.Pass));
            Assert.Throws<ArgumentNullException>(() => TutorQuizChecker.CheckAnswer(Report(), null!, RuleStatus.Pass));
        });
    }
}
