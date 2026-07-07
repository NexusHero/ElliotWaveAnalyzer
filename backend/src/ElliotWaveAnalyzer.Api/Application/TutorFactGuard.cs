using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for the Elliott-Wave tutor (#190, sibling of
/// <see cref="PositionFactGuard"/>/<see cref="AnalogFactGuard"/>/<see cref="SentimentFactGuard"/>/
/// <see cref="ThesisFactGuard"/>/<see cref="ContextFactGuard"/>/<see cref="LessonFactGuard"/>): a
/// <see cref="TutorExplanationDraft"/> may only narrate rules that actually exist in the report being
/// explained — a narrative for a rule name the report never mentions is rejected (AC1). The verdict
/// itself (<see cref="RuleStatus"/>) is never on the draft at all (see <see cref="TutorExplanationDraft"/>'s
/// remarks) — the injectable surface this guard closes is a fabricated rule name, not a fabricated
/// verdict, which is structurally impossible by the draft's own shape (AC2). Pure and static so the
/// guard is exhaustively unit-testable.
/// </summary>
public static class TutorFactGuard
{
    /// <summary>
    /// True when every <see cref="TutorRuleNarrative.RuleName"/> in <paramref name="draft"/> names a
    /// real rule in <paramref name="report"/>. A draft with no narratives passes trivially.
    /// </summary>
    public static bool Passes(TutorExplanationDraft draft, WaveRuleReport report)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(report);

        if (draft.RuleNarratives is not { Count: > 0 } narratives)
        {
            return true;
        }

        var realRuleNames = report.Rules.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        return narratives.All(n => realRuleNames.Contains(n.RuleName));
    }
}
