using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Checks a tutor quiz answer against the deterministic rule report, never the LLM's opinion (#190,
/// AC4): "Quiz me" asks the learner whether a named rule passed, failed, or is indeterminate for the
/// count on screen, and the answer is right only if it matches <see cref="RuleResult.Status"/> exactly.
/// Pure and static — no LLM call.
/// </summary>
public static class TutorQuizChecker
{
    /// <summary>True when <paramref name="answeredStatus"/> matches the named rule's real status.</summary>
    public static bool CheckAnswer(WaveRuleReport report, string ruleName, RuleStatus answeredStatus)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(ruleName);

        var rule = report.Rules.FirstOrDefault(r => r.Name == ruleName);
        return rule is not null && rule.Status == answeredStatus;
    }
}
