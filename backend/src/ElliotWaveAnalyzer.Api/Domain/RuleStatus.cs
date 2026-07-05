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
