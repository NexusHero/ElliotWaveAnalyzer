namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>A single canonical Elliott Wave rule evaluated against the user's pivots.</summary>
public sealed record RuleResult(string Name, RuleStatus Status, string Detail)
{
    /// <summary>
    /// True for guidelines (typical but not mandatory, e.g. "zigzag C extends beyond A").
    /// A failing guideline flavors the count; only a failing hard rule (the default)
    /// invalidates it — candidate generation and parsing prune on hard failures alone.
    /// </summary>
    public bool IsGuideline { get; init; }
}
