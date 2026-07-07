namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// One rule's assembled tutor explanation (#190) — produced only by
/// <see cref="Application.TutorLessonAssembler"/>. <see cref="Status"/> is always copied verbatim from
/// the engine's own <see cref="RuleResult.Status"/>, never sourced from an LLM draft, so it cannot
/// contradict the deterministic verdict (AC1, AC2). <see cref="Explanation"/> is the LLM's narrative
/// when one matched this rule, or the engine's own plain-language <see cref="RuleResult.Detail"/> as a
/// static fallback (AC6) — never blank.
/// </summary>
/// <param name="RuleName">The rule's name, matching a <see cref="RuleResult.Name"/>.</param>
/// <param name="Status">The engine's own verdict for this rule.</param>
/// <param name="Explanation">Plain-language explanation — LLM-authored or the static engine detail.</param>
public sealed record TutorRuleExplanation(string RuleName, RuleStatus Status, string Explanation);
