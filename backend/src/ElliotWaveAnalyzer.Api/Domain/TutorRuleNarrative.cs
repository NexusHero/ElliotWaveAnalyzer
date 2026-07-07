namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One LLM-authored plain-language explanation of a named rule, for the tutor's "Explain my count"
/// mode (#190). Deliberately carries no <see cref="RuleStatus"/> field — the LLM can explain *why* a
/// rule holds or fails in its own words, but it can never state the verdict itself; the verdict shown
/// to the learner always comes from <see cref="WaveRuleReport"/> via <see cref="Application.TutorLessonAssembler"/>,
/// never from this draft (AC1, AC2).
/// </summary>
/// <param name="RuleName">Must match a <see cref="RuleResult.Name"/> from the report being explained.</param>
/// <param name="Explanation">Plain-language explanation of why the rule holds or fails.</param>
public sealed record TutorRuleNarrative(string RuleName, string Explanation);
