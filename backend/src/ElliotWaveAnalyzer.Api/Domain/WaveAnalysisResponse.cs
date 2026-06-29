namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The full response of a wave-analysis request: the objective deterministic rule report,
/// the LLM's qualitative coaching assessment, and the token usage of the call.
/// </summary>
/// <param name="Result">The LLM's coaching assessment (mirrors the deterministic verdict).</param>
/// <param name="RuleReport">The deterministic, math-only rule + Fibonacci report.</param>
/// <param name="Levels">Deterministic forward levels (invalidation, support/target zones); null if undeterminable.</param>
/// <param name="Usage">Token usage for the LLM call.</param>
public sealed record WaveAnalysisResponse(
    WaveValidationResult Result,
    WaveRuleReport RuleReport,
    WaveLevels? Levels,
    TokenUsage Usage);
