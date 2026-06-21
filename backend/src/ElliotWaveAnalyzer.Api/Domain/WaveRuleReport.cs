namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Outcome of a deterministic rule check.</summary>
public enum RuleStatus
{
    /// <summary>The rule holds for the given pivots.</summary>
    Pass,

    /// <summary>The rule is violated.</summary>
    Fail,

    /// <summary>Not enough pivots to decide (e.g. wave 5 not annotated yet).</summary>
    Indeterminate,
}

/// <summary>A single canonical Elliott Wave rule evaluated against the user's pivots.</summary>
public sealed record RuleResult(string Name, RuleStatus Status, string Detail);

/// <summary>A computed Fibonacci ratio between waves (e.g. wave-2 retracement of wave 1).</summary>
public sealed record FibRatio(string Name, decimal Ratio);

/// <summary>
/// The deterministic, math-only assessment of a wave count: the three hard Elliott rules
/// plus key Fibonacci ratios. Computed in code (no LLM) so it is objective and reliable;
/// the LLM uses it as ground truth for its qualitative reflection.
/// </summary>
public sealed record WaveRuleReport(
    bool BullishAssumed,
    IReadOnlyList<RuleResult> Rules,
    IReadOnlyList<FibRatio> Ratios);
