namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Gemini's assessment of an Elliott Wave count against the canonical rules.
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
/// <param name="TokenUsage">
/// Token consumption for this call. Null only in tests where the LLM is mocked.
/// Included so clients can display cost information without a separate API call.
/// </param>
public sealed record WaveValidationResult(
    bool IsValid,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Warnings,
    string Analysis,
    string Confidence,
    TokenUsage? TokenUsage = null);
