namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The LLM's assessment of an Elliott Wave count against the canonical rules.
/// Pure domain result — token usage (an operational concern) is carried separately
/// in <see cref="LlmValidation"/> so this type stays free of infrastructure details.
/// </summary>
/// <param name="IsValid">
/// True only when zero violations are found.
/// Warnings do not affect validity — they flag borderline or ambiguous patterns.
/// </param>
/// <param name="Violations">
/// Hard rule breaches that invalidate the count
/// (e.g. "Wave 3 is shorter than Wave 1 and Wave 5").
/// </param>
/// <param name="Warnings">
/// Soft flags — technically valid but unusual patterns
/// (e.g. "Wave 5 falls significantly short of Wave 3 — possible truncation").
/// </param>
/// <param name="Analysis">Brief narrative of the overall wave structure.</param>
/// <param name="Confidence">LLM self-assessed confidence: "high", "medium", or "low".</param>
public sealed record WaveValidationResult(
    bool IsValid,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings,
    string Analysis,
    string Confidence);
