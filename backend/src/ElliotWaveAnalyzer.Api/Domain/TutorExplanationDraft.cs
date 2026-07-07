namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The LLM's only output for the tutor's "Explain my count" mode (#190) — untrusted until
/// <see cref="Application.TutorFactGuard"/> and <see cref="Application.TutorLessonAssembler"/> process
/// it. Has no field for an overall validity verdict at all: that always comes from the engine's own
/// <see cref="WaveRuleReport"/> (AC2), the same no-free-text-for-the-dangerous-part discipline as
/// <see cref="ScanQueryDraft"/> (#185).
/// </summary>
/// <param name="RuleNarratives">Per-rule plain-language explanations, or null/empty with no LLM key.</param>
/// <param name="Summary">An overall plain-language summary, or null with no LLM key.</param>
public sealed record TutorExplanationDraft(IReadOnlyList<TutorRuleNarrative>? RuleNarratives, string? Summary);
