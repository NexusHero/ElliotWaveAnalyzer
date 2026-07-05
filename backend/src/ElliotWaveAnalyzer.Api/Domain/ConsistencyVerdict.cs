namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The verdict for one parent→child link in a top-down chain: how well the finer-timeframe count
/// fits inside the higher-timeframe wave it is supposed to be elaborating.
/// </summary>
public enum ConsistencyVerdict
{
    /// <summary>Finer count matches the parent's direction, class and price window.</summary>
    Consistent,

    /// <summary>Direction agrees, but the class or price window is off — a workable but imperfect fit.</summary>
    Tension,

    /// <summary>No finer count travels the parent's direction — the timeframes disagree.</summary>
    Contradiction,
}
