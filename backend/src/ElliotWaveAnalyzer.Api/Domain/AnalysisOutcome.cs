namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// How a tracked analysis has played out since it was saved, decided by the first of these
/// events in the candles that followed. Serialized by name.
/// </summary>
public enum AnalysisOutcome
{
    /// <summary>Neither the invalidation nor the target has been touched yet — still unfolding.</summary>
    Pending,

    /// <summary>Price crossed the invalidation line: the count is void.</summary>
    Invalidated,

    /// <summary>Price entered the projected target zone before invalidating.</summary>
    TargetReached,
}
