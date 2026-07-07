namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The tutor's "Explain my count" output (#190): the engine's own validity verdict, a per-rule
/// explanation for every rule in the report, and an optional overall summary. Assembled only by
/// <see cref="Application.TutorLessonAssembler"/> from a <see cref="WaveRuleReport"/> — read-only, it
/// never touches the count, pivots or levels (AC3).
/// </summary>
/// <param name="IsValid">True when no rule in the report is a hard <see cref="RuleStatus.Fail"/> —
/// copied from the engine's own verdict, never asserted independently by the LLM (AC2).</param>
/// <param name="RuleExplanations">One entry per rule in the report, in report order.</param>
/// <param name="Summary">An overall LLM-authored summary, or null when unavailable.</param>
/// <param name="SummaryUnavailableReason">Why <see cref="Summary"/> is absent, or null when present.</param>
public sealed record TutorLesson(
    bool IsValid,
    IReadOnlyList<TutorRuleExplanation> RuleExplanations,
    string? Summary,
    string? SummaryUnavailableReason);
