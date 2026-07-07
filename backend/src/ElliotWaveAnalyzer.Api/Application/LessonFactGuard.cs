using ElliotWaveAnalyzer.Api.Domain;

namespace ElliotWaveAnalyzer.Api.Application;

/// <summary>
/// The anti-hallucination guard for self-improving confidence (#189, sibling of
/// <see cref="PositionFactGuard"/>/<see cref="AnalogFactGuard"/>/<see cref="SentimentFactGuard"/>/
/// <see cref="ThesisFactGuard"/>/<see cref="ContextFactGuard"/>): a <see cref="Lesson"/> is evidence,
/// not narrative — every id in <see cref="Lesson.SupportingCaseIds"/> must name a real recorded
/// <see cref="MissCase"/>, and a lesson citing even one non-existent case is rejected wholesale (AC2)
/// rather than partially honoured. A lesson with no supporting cases at all is not evidence-linked and
/// is rejected the same way. Pure and static so the guard is exhaustively unit-testable.
/// </summary>
public static class LessonFactGuard
{
    /// <summary>
    /// Bound on <see cref="Lesson.SuggestedWeightNudge"/> — a lesson can only ever nudge a future
    /// ranking weight by a small amount, never propose an extreme swing.
    /// </summary>
    public const decimal MaxWeightNudge = 0.2m;

    /// <summary>
    /// True when every one of <paramref name="lesson"/>'s supporting case ids names a real recorded
    /// miss in <paramref name="realCaseIds"/>, there is at least one, and any proposed weight nudge is
    /// within <see cref="MaxWeightNudge"/>.
    /// </summary>
    public static bool Passes(Lesson lesson, IReadOnlyCollection<Guid> realCaseIds)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        ArgumentNullException.ThrowIfNull(realCaseIds);

        if (lesson.SupportingCaseIds.Count == 0)
        {
            return false;
        }

        var validIds = new HashSet<Guid>(realCaseIds);
        if (!lesson.SupportingCaseIds.All(validIds.Contains))
        {
            return false;
        }

        return lesson.SuggestedWeightNudge is not { } nudge || Math.Abs(nudge) <= MaxWeightNudge;
    }
}
