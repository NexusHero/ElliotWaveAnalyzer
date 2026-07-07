using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// Assembles a <see cref="TutorLesson"/> from the engine's own <see cref="WaveRuleReport"/> and an
/// optional LLM <see cref="TutorExplanationDraft"/> (#190). Pure composition — every rule's
/// <see cref="TutorRuleExplanation.Status"/> is copied verbatim from the report, never influenced by
/// the draft, so the assembled lesson can never contradict the deterministic verdict (AC1, AC2). With
/// no draft (no LLM key) or no narrative for a given rule, falls back to the engine's own
/// plain-language <see cref="RuleResult.Detail"/> — a static, deterministic explanation, never a blank
/// or dead feature (AC6).
/// </summary>
public static class TutorLessonAssembler
{
    /// <summary>Assembles a lesson explaining <paramref name="report"/>, using <paramref name="draft"/> when available.</summary>
    public static TutorLesson Assemble(WaveRuleReport report, TutorExplanationDraft? draft)
    {
        ArgumentNullException.ThrowIfNull(report);

        var narrativeByRule = (draft?.RuleNarratives ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n.RuleName) && !string.IsNullOrWhiteSpace(n.Explanation))
            .GroupBy(n => n.RuleName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Explanation, StringComparer.Ordinal);

        var explanations = report.Rules
            .Select(rule => new TutorRuleExplanation(
                rule.Name,
                rule.Status,
                narrativeByRule.TryGetValue(rule.Name, out var narrative) ? narrative : rule.Detail))
            .ToList();

        var isValid = report.Rules.All(r => r.Status != RuleStatus.Fail);

        var summary = string.IsNullOrWhiteSpace(draft?.Summary) ? null : draft.Summary;
        var summaryUnavailableReason = summary is null
            ? "No LLM narrative available; showing the deterministic rule report."
            : null;

        return new TutorLesson(isValid, explanations, summary, summaryUnavailableReason);
    }
}
