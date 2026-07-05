namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The deterministic, math-only assessment of a wave count: the three hard Elliott rules
/// plus key Fibonacci ratios. Computed in code (no LLM) so it is objective and reliable;
/// the LLM uses it as ground truth for its qualitative reflection.
/// </summary>
public sealed record WaveRuleReport(
    bool BullishAssumed,
    IReadOnlyList<RuleResult> Rules,
    IReadOnlyList<FibRatio> Ratios);
