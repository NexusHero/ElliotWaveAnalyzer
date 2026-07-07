namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// An LLM-proposed lesson from reflecting on recorded misses (#189): a categorized hypothesis tied to
/// the concrete <see cref="MissCase"/>s that support it, plus an optional bounded nudge to future
/// ranking weight. Never applied on its own — <see cref="Application.LessonFactGuard"/> must pass it
/// first, and applying it (a follow-on integration slice) stays human-gated and cannot reach the hard
/// Elliott rules or the deterministic geometry (ADR-009).
/// </summary>
/// <param name="Category">Short classification, e.g. "shallow-wave-2" or "low-volatility-regime".</param>
/// <param name="Hypothesis">The proposed pattern in the misses, in the LLM's own words.</param>
/// <param name="SupportingCaseIds">The recorded misses this lesson claims to generalize from.</param>
/// <param name="SuggestedWeightNudge">
/// A bounded nudge to a future ranking weight (see <see cref="Application.LessonFactGuard.MaxWeightNudge"/>),
/// or null when the lesson doesn't propose one.
/// </param>
public sealed record Lesson(
    string Category,
    string Hypothesis,
    IReadOnlyList<Guid> SupportingCaseIds,
    decimal? SuggestedWeightNudge);
