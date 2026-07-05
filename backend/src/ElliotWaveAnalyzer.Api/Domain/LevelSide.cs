namespace ElliotWaveAnalyzer.Api.Domain;

/// <summary>Which side of the current price a level sits on.</summary>
public enum LevelSide
{
    /// <summary>The level is below current price (support / floor in a bullish count).</summary>
    Below,

    /// <summary>The level is above current price (resistance / cap in a bearish count).</summary>
    Above,
}
