namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Token consumption for a single LLM API call.
/// Returned by every <see cref="Interfaces.ILlmWaveAnalyzer"/> implementation
/// and embedded in <see cref="WaveValidationResult"/> so the caller sees
/// the cost of each validation request.
/// </summary>
public sealed record TokenUsage(
    string Provider,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);
