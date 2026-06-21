namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// Pairs the pure <see cref="WaveValidationResult"/> with the <see cref="TokenUsage"/>
/// of the LLM call that produced it.
///
/// WHY a wrapper instead of embedding usage in the result:
/// the wave assessment is domain data; token cost is operational telemetry. Keeping
/// them separate lets the domain result stay provider-agnostic while still letting the
/// API surface cost information to the client in one response.
/// </summary>
public sealed record LlmValidation(WaveValidationResult Result, TokenUsage Usage);
