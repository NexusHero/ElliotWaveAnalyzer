namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>
/// The net price direction a wave (or a whole timeframe's move) travels. Used by the top-down
/// analyzer to decide whether a finer-timeframe count can live inside a higher-timeframe wave:
/// a finer count that travels the wrong way contradicts its parent and is rejected.
/// </summary>
public enum TrendDirection
{
    /// <summary>Net upward move (end price above start price).</summary>
    Up,

    /// <summary>Net downward move (end price below start price).</summary>
    Down,
}
